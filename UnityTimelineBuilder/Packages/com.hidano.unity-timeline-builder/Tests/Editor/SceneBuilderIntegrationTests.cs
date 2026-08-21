using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.TestTools;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class SceneBuilderIntegrationTests
    {
        private const string TemplatePath = "Packages/com.hidano.unity-timeline-builder/Documentation~/timeline-template.csv";
        private const string FixtureDirectory = "Assets/UnityTimelineBuilder/Tests/SceneBuilderIntegrationFixtures";
        private const string LegacySheetPath = FixtureDirectory + "/legacy.csv";
        private const string AnimationPath = FixtureDirectory + "/character.anim";
        private const string ScenePrefabPath = FixtureDirectory + "/Character.prefab";
        private const string TemplateAudioPath = "Assets/Audio/intro.wav";
        private const string TemplateAnimationPath = "Assets/Animations/character.fbx";
        private const string TemplatePrefabPath = "Assets/Prefabs/Character.prefab";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/SceneBuilderIntegrationOutput";
        private const string TemplateScenePath = OutputDirectory + "/SampleScene.unity";
        private const string LegacyTimelinePath = OutputDirectory + "/Legacy.playable";
        private const string LegacyPrefabPath = OutputDirectory + "/Legacy.prefab";
        private const string LegacyScenePath = OutputDirectory + "/LegacyScene.unity";
        private AnimationClip animationFixture;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(FixtureDirectory);
            EnsureFolder(OutputDirectory);
            EnsureFolder("Assets/Audio");
            EnsureFolder("Assets/Animations");
            EnsureFolder("Assets/Prefabs");

            File.WriteAllBytes(ProjectPath(TemplateAudioPath), CreateSilentWave(48000));
            AssetDatabase.ImportAsset(TemplateAudioPath, ImportAssetOptions.ForceSynchronousImport);

            var character = new GameObject("Character");
            var characterRoot = new GameObject("CharacterRoot");
            characterRoot.transform.SetParent(character.transform);
            characterRoot.AddComponent<Animator>();
            PrefabUtility.SaveAsPrefabAsset(character, ScenePrefabPath);
            PrefabUtility.SaveAsPrefabAsset(character, TemplatePrefabPath);
            UnityEngine.Object.DestroyImmediate(character);

            animationFixture = new AnimationClip { name = "character" };
            animationFixture.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0, 0, 1, 1));
            AssetDatabase.CreateAsset(animationFixture, AnimationPath);
            ResourceResolverRegistry.Register(new TestAnimationResolver(animationFixture));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(OutputDirectory);
            AssetDatabase.DeleteAsset(ScenePrefabPath);
            AssetDatabase.DeleteAsset(AnimationPath);
            AssetDatabase.DeleteAsset(TemplatePrefabPath);
            AssetDatabase.DeleteAsset(TemplateAudioPath);
            if (File.Exists(ProjectPath(LegacySheetPath)))
                File.Delete(ProjectPath(LegacySheetPath));
            foreach (var sheet in new[] { "missing-prefab.csv", "missing-timeline.csv" })
            {
                var path = ProjectPath(FixtureDirectory + "/" + sheet);
                if (File.Exists(path))
                    File.Delete(path);
            }
            AssetDatabase.DeleteAsset(FixtureDirectory);
            ResourceResolverRegistry.ResetForTest();
            UnityEngine.Object.DestroyImmediate(animationFixture);
            animationFixture = null;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void BuildsUnitySceneFromBundledTemplateAndPreservesBinding()
        {
            var templateBeforeBuild = File.ReadAllText(ProjectPath(TemplatePath));
            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = ProjectPath(TemplatePath),
                OutputDirectory = OutputDirectory,
                AssetName = "BundledTemplate",
                ImportDirectory = FixtureDirectory + "/Imported"
            });

            Assert.That(result.Success, Is.True, FormatErrors(result));
            Assert.That(result.ScenePath, Is.EqualTo(TemplateScenePath));
            Assert.That(File.ReadAllText(ProjectPath(TemplatePath)), Is.EqualTo(templateBeforeBuild));
            Assert.That(File.Exists(ProjectPath(TemplateScenePath)), Is.True);

            var scene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Single);
            var directorObject = scene.GetRootGameObjects().Single(root => root.name == "BundledTemplate");
            var director = directorObject.GetComponent<PlayableDirector>();
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(OutputDirectory + "/BundledTemplate.playable");
            var character = scene.GetRootGameObjects().Single(root => root.name == "Character");

            Assert.That(director, Is.Not.Null);
            Assert.That(director.playableAsset, Is.SameAs(timeline));
            Assert.That(PrefabUtility.IsAnyPrefabInstanceRoot(character), Is.True);
            Assert.That(character.GetComponentInChildren<Animator>(), Is.Not.Null);
            var characterTrack = timeline.GetOutputTracks().OfType<AnimationTrack>()
                .Single(track => track.name == "Character");
            Assert.That(director.GetGenericBinding(characterTrack), Is.SameAs(
                character.GetComponentInChildren<Animator>()));
        }

        [Test]
        public void BuildsLegacyCsvWithoutSceneAndLeavesScenePathNull()
        {
            File.WriteAllText(ProjectPath(LegacySheetPath),
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath\n" +
                "Animation,Character,character,0,0,1," + AnimationPath + "\n");
            AssetDatabase.Refresh();

            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = ProjectPath(LegacySheetPath),
                OutputDirectory = OutputDirectory,
                AssetName = "Legacy",
                ImportDirectory = FixtureDirectory + "/Imported"
            });

            Assert.That(result.Success, Is.True, FormatErrors(result));
            Assert.That(result.ScenePath, Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<TimelineAsset>(LegacyTimelinePath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(LegacyPrefabPath), Is.Not.Null);
            Assert.That(File.Exists(ProjectPath(LegacyScenePath)), Is.False);
        }

        [Test]
        public void StopsBeforeGeneratingAssetsWhenScenePrefabIsMissing()
        {
            var sheetPath = FixtureDirectory + "/missing-prefab.csv";
            File.WriteAllText(ProjectPath(sheetPath),
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath\n" +
                "Animation,Character,character,0,0,1," + AnimationPath + "\n" +
                "Scene,MissingPrefabScene,\n" +
                "ScenePrefab,,,,,," + FixtureDirectory + "/Missing.prefab\n");
            AssetDatabase.Refresh();

            LogAssert.ignoreFailingMessages = true;
            BuildResult result;
            try
            {
                result = TimelineBuilder.Build(new BuildRequest
                {
                    SheetPath = ProjectPath(sheetPath),
                    OutputDirectory = OutputDirectory,
                    AssetName = "MissingPrefab",
                    ImportDirectory = FixtureDirectory + "/Imported"
                });
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }

            Assert.That(result.Success, Is.False);
            Assert.That(result.TimelineAssetPath, Is.Null);
            Assert.That(result.PrefabPath, Is.Null);
            Assert.That(result.ScenePath, Is.Null);
            Assert.That(result.Errors.Any(error => error.Code == BuildErrorCode.ScenePrefabInvalid), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<TimelineAsset>(OutputDirectory + "/MissingPrefab.playable"), Is.Null);
        }

        [Test]
        public void StopsBeforeGeneratingAssetsWhenExplicitTimelineIsMissing()
        {
            var sheetPath = FixtureDirectory + "/missing-timeline.csv";
            File.WriteAllText(ProjectPath(sheetPath),
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath\n" +
                "Animation,Character,character,0,0,1," + AnimationPath + "\n" +
                "Scene,MissingTimelineScene,,,,,Assets/MissingTimeline.playable\n" +
                "ScenePrefab,,,,,," + ScenePrefabPath + "\n");
            AssetDatabase.Refresh();

            LogAssert.ignoreFailingMessages = true;
            BuildResult result;
            try
            {
                result = TimelineBuilder.Build(new BuildRequest
                {
                    SheetPath = ProjectPath(sheetPath),
                    OutputDirectory = OutputDirectory,
                    AssetName = "MissingTimeline",
                    ImportDirectory = FixtureDirectory + "/Imported"
                });
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }

            Assert.That(result.Success, Is.False);
            Assert.That(result.TimelineAssetPath, Is.Null);
            Assert.That(result.ScenePath, Is.Null);
            Assert.That(result.Errors.Any(error => error.Code == BuildErrorCode.SceneTimelineNotFound), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<TimelineAsset>(OutputDirectory + "/MissingTimeline.playable"), Is.Null);
        }

        private static string ProjectPath(string path)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                path.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string FormatErrors(BuildResult result)
        {
            return string.Join("\n", result.Errors.Select(error => error.Code + ": " + error.Message));
        }

        private static byte[] CreateSilentWave(int sampleCount)
        {
            const short channels = 1;
            const short bitsPerSample = 16;
            const int sampleRate = 48000;
            var dataSize = sampleCount * channels * (bitsPerSample / 8);
            using (var stream = new MemoryStream(44 + dataSize))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * bitsPerSample / 8);
                writer.Write((short)(channels * bitsPerSample / 8));
                writer.Write(bitsPerSample);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                writer.Write(new byte[dataSize]);
                return stream.ToArray();
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private sealed class TestAnimationResolver : IResourceResolver
        {
            private readonly AnimationClip clip;

            public TestAnimationResolver(AnimationClip clip)
            {
                this.clip = clip;
            }

            public string ResourceKind => "Animation";
            public Type AssetType => typeof(AnimationClip);

            public bool TryResolve(ClipRow row, ResolveContext context,
                out UnityEngine.Object asset, out BuildError error)
            {
                asset = null;
                error = null;
                if (row != null && (string.Equals(row.ResourcePath, AnimationPath, StringComparison.Ordinal)
                    || string.Equals(row.ResourcePath, TemplateAnimationPath, StringComparison.Ordinal)))
                {
                    asset = clip;
                    return true;
                }

                error = new BuildError(BuildErrorCode.ResourceNotFound,
                    row == null ? (int?)null : row.LineNumber,
                    row == null ? null : row.ResourcePath,
                    "Test animation fixture could not be resolved.");
                return false;
            }
        }
    }
}
