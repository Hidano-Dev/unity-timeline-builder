using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class PrefabBuilderIntegrationTests
    {
        private const string FixtureSheetPath = FixtureDirectory + "/integration.csv";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/PrefabIntegrationOutput";
        private const string FixtureDirectory = "Assets/UnityTimelineBuilder/Tests/PrefabIntegrationFixtures";
        private const string AudioAssetPath = FixtureDirectory + "/integration.wav";
        private const string AnimationAssetPath = FixtureDirectory + "/integration.anim";
        private const string TimelineAssetPath = OutputDirectory + "/PrefabIntegration.playable";
        private const string PrefabAssetPath = OutputDirectory + "/PrefabIntegration.prefab";

        [SetUp]
        public void SetUp()
        {
            EnsureFolder("Assets/UnityTimelineBuilder");
            EnsureFolder("Assets/UnityTimelineBuilder/Tests");
            EnsureFolder(FixtureDirectory);
            EnsureFolder(OutputDirectory);

            File.WriteAllBytes(ProjectPath(AudioAssetPath), CreateSilentWave(800));
            AssetDatabase.ImportAsset(AudioAssetPath, ImportAssetOptions.ForceSynchronousImport);
            File.WriteAllText(ProjectPath(FixtureSheetPath),
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath\n"
                + "Audio,Music,Intro,0,0.25,2," + AudioAssetPath + "\n"
                + "Animation,Character,Walk,1,0.5,3," + AnimationAssetPath + "\n");
            var animation = new AnimationClip { name = "PrefabIntegrationAnimation" };
            animation.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0, 0, 1, 1));
            AssetDatabase.CreateAsset(animation, AnimationAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(PrefabAssetPath);
            AssetDatabase.DeleteAsset(TimelineAssetPath);
            AssetDatabase.DeleteAsset(OutputDirectory);
            AssetDatabase.DeleteAsset(FixtureDirectory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void BuildsPrefabWithDirectorAndPreservesAssetsAcrossIdempotentOverwrite()
        {
            var first = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = ProjectPath(FixtureSheetPath),
                OutputDirectory = OutputDirectory,
                AssetName = "PrefabIntegration",
                ImportDirectory = "Assets/UnityTimelineBuilder/Tests/PrefabIntegrationImported"
            });

            Assert.That(first.Success, Is.True, FormatErrors(first));
            Assert.That(first.TimelineAssetPath, Is.EqualTo(TimelineAssetPath));
            Assert.That(first.PrefabPath, Is.EqualTo(PrefabAssetPath));

            var firstTimelineGuid = AssetDatabase.AssetPathToGUID(TimelineAssetPath);
            var firstPrefabGuid = AssetDatabase.AssetPathToGUID(PrefabAssetPath);
            AssertPrefabReferencesTimelineAndHasNoBindings();
            Assert.That(GameObject.Find("PrefabIntegration"), Is.Null);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(".*Overwriting TimelineAsset.*"));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(".*Overwriting Prefab.*"));

            var second = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = ProjectPath(FixtureSheetPath),
                OutputDirectory = OutputDirectory,
                AssetName = "PrefabIntegration",
                ImportDirectory = "Assets/UnityTimelineBuilder/Tests/PrefabIntegrationImported"
            });

            Assert.That(second.Success, Is.True, FormatErrors(second));
            Assert.That(AssetDatabase.AssetPathToGUID(TimelineAssetPath), Is.EqualTo(firstTimelineGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(PrefabAssetPath), Is.EqualTo(firstPrefabGuid));
            AssertPrefabReferencesTimelineAndHasNoBindings();
            Assert.That(GameObject.Find("PrefabIntegration"), Is.Null);
        }

        private static void AssertPrefabReferencesTimelineAndHasNoBindings()
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelineAssetPath);
            Assert.That(timeline, Is.Not.Null);

            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabAssetPath);
            try
            {
                var director = prefabRoot.GetComponent<PlayableDirector>();
                Assert.That(director, Is.Not.Null);
                Assert.That(director.playableAsset, Is.SameAs(timeline));
                Assert.That(timeline.GetOutputTracks(), Is.Not.Empty);
                Assert.That(timeline.GetOutputTracks().All(track => director.GetGenericBinding(track) == null), Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static string ProjectPath(string path)
        {
            return System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName,
                path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        }

        private static string FormatErrors(BuildResult result)
        {
            return string.Join("\n", result.Errors.Select(error => error.Code + ": " + error.Message));
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

        private static byte[] CreateSilentWave(int sampleCount)
        {
            const short channels = 1;
            const short bitsPerSample = 16;
            const int sampleRate = 8000;
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
    }
}
