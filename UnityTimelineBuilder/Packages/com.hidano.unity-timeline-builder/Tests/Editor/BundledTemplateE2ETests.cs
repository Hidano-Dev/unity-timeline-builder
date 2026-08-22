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
    public sealed class BundledTemplateE2ETests
    {
        private const string TemplatePath = "Packages/com.hidano.unity-timeline-builder/Documentation~/timeline-template.csv";
        private const string AudioDirectory = "Assets/Audio";
        private const string AnimationDirectory = "Assets/Animations";
        private const string AudioPath = AudioDirectory + "/intro.wav";
        private const string AnimationPath = AnimationDirectory + "/character.fbx";
        private const string PrefabDirectory = "Assets/Prefabs";
        private const string ScenePrefabPath = PrefabDirectory + "/Character.prefab";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/TemplateE2EOutput";
        private const string TimelinePath = OutputDirectory + "/SampleScene/Timelines/BundledTemplate.playable";
        private const string PrefabPath = OutputDirectory + "/SampleScene/Prefabs/BundledTemplate.prefab";
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

            var characterRoot = new GameObject("Character");
            var animatorObject = new GameObject("CharacterRoot");
            animatorObject.transform.SetParent(characterRoot.transform);
            animatorObject.AddComponent<Animator>();
            PrefabUtility.SaveAsPrefabAsset(characterRoot, ScenePrefabPath);
            UnityEngine.Object.DestroyImmediate(characterRoot);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

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
            AssetDatabase.DeleteAsset(PrefabPath);
            AssetDatabase.DeleteAsset(TimelinePath);
            if (!scenePrefabExisted)
                AssetDatabase.DeleteAsset(ScenePrefabPath);
            if (!audioFileExisted)
                AssetDatabase.DeleteAsset(AudioPath);
            AssetDatabase.DeleteAsset(OutputDirectory);
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
        public void BuildsSuccessfullyFromBundledTemplateWithoutChangingItsInput()
        {
            var templateBeforeBuild = File.ReadAllText(ProjectPath(TemplatePath));

            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = ProjectPath(TemplatePath),
                OutputDirectory = OutputDirectory,
                AssetName = "BundledTemplate",
                ImportDirectory = "Assets/UnityTimelineBuilder/Imported"
            });

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True,
                string.Join("\n", result.Errors.Select(error => error.Message)));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(File.ReadAllText(ProjectPath(TemplatePath)), Is.EqualTo(templateBeforeBuild));

            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            Assert.That(timeline, Is.Not.Null);
            var tracks = timeline.GetOutputTracks().ToArray();
            Assert.That(tracks, Has.Length.EqualTo(2));

            var audioTrack = tracks.Single(track => track is AudioTrack);
            Assert.That(audioTrack.name, Is.EqualTo("BGM"));
            var audioClip = audioTrack.GetClips().Single();
            Assert.That(audioClip.start, Is.EqualTo(0).Within(0.0001));
            Assert.That(audioClip.duration, Is.EqualTo(3.2).Within(0.0001));
            Assert.That(audioClip.displayName, Is.EqualTo("intro"));
            Assert.That(((AudioPlayableAsset)audioClip.asset).clip,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath)));

            var animationTrack = tracks.Single(track => track is AnimationTrack);
            Assert.That(animationTrack.name, Is.EqualTo("Character"));
            var animationClip = animationTrack.GetClips().Single();
            Assert.That(animationClip.start, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(animationClip.duration, Is.EqualTo(2.5).Within(0.0001));
            Assert.That(animationClip.displayName, Is.EqualTo("intro"));
            Assert.That(((AnimationPlayableAsset)animationClip.asset).clip,
                Is.SameAs(animationFixture));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var director = prefab.GetComponent<PlayableDirector>();
            Assert.That(director, Is.Not.Null);
            Assert.That(director.playableAsset, Is.SameAs(timeline));
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
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private sealed class TemplateAnimationResolver : IResourceResolver
        {
            private readonly AnimationClip clip;

            public TemplateAnimationResolver(AnimationClip clip)
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
