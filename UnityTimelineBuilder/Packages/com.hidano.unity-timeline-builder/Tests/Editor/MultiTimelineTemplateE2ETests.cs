using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class MultiTimelineTemplateE2ETests
    {
        private const string TemplatePath =
            "Packages/com.hidano.unity-timeline-builder/Documentation~/multi-timeline-template.csv";
        private const string AudioDirectory = "Assets/Audio";
        private const string AnimationDirectory = "Assets/Animations";
        private const string AudioPath = AudioDirectory + "/intro.wav";
        private const string AnimationPath = AnimationDirectory + "/character.fbx";
        private const string PrefabDirectory = "Assets/Prefabs";
        private const string ScenePrefabPath = PrefabDirectory + "/Character.prefab";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/MultiTemplateE2EOutput";
        private AnimationClip animationFixture;
        private bool audioDirectoryExisted;
        private bool prefabDirectoryExisted;
        private bool scenePrefabExisted;
        private bool audioFileExisted;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            audioDirectoryExisted = AssetDatabase.IsValidFolder(AudioDirectory);
            prefabDirectoryExisted = AssetDatabase.IsValidFolder(PrefabDirectory);
            scenePrefabExisted = File.Exists(ProjectPath(ScenePrefabPath));
            audioFileExisted = File.Exists(ProjectPath(AudioPath));
            EnsureFolder(AudioDirectory);
            EnsureFolder(PrefabDirectory);
            EnsureFolder(OutputDirectory);

            var character = new GameObject("Character");
            var characterRoot = new GameObject("CharacterRoot");
            characterRoot.transform.SetParent(character.transform);
            characterRoot.AddComponent<Animator>();
            PrefabUtility.SaveAsPrefabAsset(character, ScenePrefabPath);
            UnityEngine.Object.DestroyImmediate(character);

            File.WriteAllBytes(ProjectPath(AudioPath), CreateSilentWave(48000));
            AssetDatabase.ImportAsset(AudioPath, ImportAssetOptions.ForceSynchronousImport);

            animationFixture = new AnimationClip { name = "intro" };
            animationFixture.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0, 0, 1, 1));
            ResourceResolverRegistry.Register(new TemplateAnimationResolver(animationFixture));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(OutputDirectory);
            if (!scenePrefabExisted)
                AssetDatabase.DeleteAsset(ScenePrefabPath);
            if (!audioFileExisted)
                AssetDatabase.DeleteAsset(AudioPath);
            if (!prefabDirectoryExisted && AssetDatabase.IsValidFolder(PrefabDirectory))
                AssetDatabase.DeleteAsset(PrefabDirectory);
            if (!audioDirectoryExisted)
                AssetDatabase.DeleteAsset(AudioDirectory);
            ResourceResolverRegistry.ResetForTest();
            UnityEngine.Object.DestroyImmediate(animationFixture);
            animationFixture = null;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void BuildsBundledMultiTimelineTemplateWithoutChangingItsInput()
        {
            var templateBeforeBuild = File.ReadAllText(ProjectPath(TemplatePath));

            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = ProjectPath(TemplatePath),
                OutputDirectory = OutputDirectory,
                ImportDirectory = "Assets/UnityTimelineBuilder/Imported"
            });

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True, FormatErrors(result));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(File.ReadAllText(ProjectPath(TemplatePath)), Is.EqualTo(templateBeforeBuild));
            Assert.That(result.Outputs, Has.Count.EqualTo(2));
            Assert.That(result.Outputs.Select(output => output.TimelineName),
                Is.EqualTo(new[] { "Opening", "Battle" }));

            var opening = result.Outputs.Single(output => output.TimelineName == "Opening");
            var battle = result.Outputs.Single(output => output.TimelineName == "Battle");
            Assert.That(opening.TimelineAssetPath, Is.EqualTo(OutputDirectory + "/OpeningScene/Timelines/Opening.playable"));
            Assert.That(opening.PrefabPath, Is.EqualTo(OutputDirectory + "/OpeningScene/Prefabs/Opening.prefab"));
            Assert.That(opening.ScenePath, Is.EqualTo(OutputDirectory + "/OpeningScene/Scenes/OpeningScene.unity"));
            Assert.That(battle.TimelineAssetPath, Is.EqualTo(OutputDirectory + "/Battle/Timelines/Battle.playable"));
            Assert.That(battle.PrefabPath, Is.EqualTo(OutputDirectory + "/Battle/Prefabs/Battle.prefab"));
            Assert.That(battle.ScenePath, Is.Null);

            AssertTimeline(opening.TimelineAssetPath, "Opening", 0.5);
            AssertTimeline(battle.TimelineAssetPath, "Battle", 1.0);
            AssertPrefabReferencesTimeline(opening.PrefabPath, opening.TimelineAssetPath);
            AssertPrefabReferencesTimeline(battle.PrefabPath, battle.TimelineAssetPath);
            AssertSceneReferencesTimeline(opening.ScenePath, opening.TimelineAssetPath);
        }

        private static void AssertTimeline(string timelinePath, string expectedGroup, double animationStart)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            Assert.That(timeline, Is.Not.Null);
            var tracks = timeline.GetOutputTracks().ToArray();
            Assert.That(tracks, Has.Length.EqualTo(2));
            Assert.That(tracks.OfType<AudioTrack>().Single().name, Is.EqualTo("BGM"));
            Assert.That(tracks.OfType<AnimationTrack>().Single().name, Is.EqualTo("Character"));
            Assert.That(tracks.OfType<AudioTrack>().Single().GetClips().Single().displayName,
                Is.EqualTo("intro"));
            var animationClip = tracks.OfType<AnimationTrack>().Single().GetClips().Single();
            Assert.That(animationClip.displayName, Is.EqualTo("intro"));
            Assert.That(animationClip.start, Is.EqualTo(animationStart).Within(0.0001));
            Assert.That(animationClip.duration, Is.EqualTo(2.5).Within(0.0001));
            Assert.That(timeline.name, Is.EqualTo(expectedGroup));
        }

        private static void AssertPrefabReferencesTimeline(string prefabPath, string timelinePath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);
            var director = prefab.GetComponent<PlayableDirector>();
            Assert.That(director, Is.Not.Null);
            Assert.That(director.playableAsset,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath)));
        }

        private static void AssertSceneReferencesTimeline(string scenePath, string timelinePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            var director = scene.GetRootGameObjects()
                .Select(root => root.GetComponent<PlayableDirector>())
                .Single(component => component != null);
            Assert.That(director.playableAsset, Is.SameAs(timeline));
            var track = timeline.GetOutputTracks().OfType<AnimationTrack>().Single();
            Assert.That(director.GetGenericBinding(track), Is.Not.Null);
            Assert.That(director.GetGenericBinding(track).name, Is.EqualTo("CharacterRoot"));
        }

        private static string FormatErrors(BuildResult result)
        {
            return string.Join("\n", result.Errors.Select(error => error.Code + ": " + error.Message));
        }

        private static string ProjectPath(string path)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                path.Replace('/', Path.DirectorySeparatorChar));
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

        private sealed class TemplateAnimationResolver : IResourceResolver
        {
            private readonly AnimationClip clip;

            public TemplateAnimationResolver(AnimationClip clip) { this.clip = clip; }
            public string ResourceKind => "Animation";
            public Type AssetType => typeof(AnimationClip);

            public bool TryResolve(ClipRow row, ResolveContext context,
                out UnityEngine.Object asset, out BuildError error)
            {
                asset = null;
                error = null;
                if (row == null || context == null ||
                    !string.Equals(row.ResourcePath, AnimationPath, StringComparison.Ordinal))
                {
                    error = new BuildError(BuildErrorCode.ResourceNotFound,
                        row == null ? (int?)null : row.LineNumber,
                        row == null ? null : row.ResourcePath,
                        "Template animation fixture could not be resolved.");
                    return false;
                }

                asset = clip;
                return true;
            }
        }
    }
}
