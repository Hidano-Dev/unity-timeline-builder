using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class TimelineBuilderIntegrationTests
    {
        private const string FixtureDirectory = "Assets/UnityTimelineBuilder/Tests/Fixtures";
        private const string AudioAssetPath = FixtureDirectory + "/TimelineIntegrationAudio.wav";
        private const string AnimationAssetPath = FixtureDirectory + "/TimelineIntegrationAnimation.anim";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/IntegrationOutput";
        private const string TimelineAssetPath = OutputDirectory + "/TimelineIntegration.playable";
        private const string PrefabAssetPath = OutputDirectory + "/TimelineIntegration.prefab";

        [SetUp]
        public void SetUp()
        {
            EnsureFolder(FixtureDirectory);
            EnsureFolder(OutputDirectory);

            File.WriteAllBytes(GetProjectPath(AudioAssetPath), CreateSilentWave(48000));
            AssetDatabase.ImportAsset(AudioAssetPath, ImportAssetOptions.ForceSynchronousImport);

            var animation = new AnimationClip { name = "TimelineIntegrationAnimation" };
            animation.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0, 0, 2, 1));
            AssetDatabase.CreateAsset(animation, AnimationAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(PrefabAssetPath);
            AssetDatabase.DeleteAsset(TimelineAssetPath);
            AssetDatabase.DeleteAsset(AudioAssetPath);
            AssetDatabase.DeleteAsset(AnimationAssetPath);
            AssetDatabase.DeleteAsset(OutputDirectory);
            AssetDatabase.DeleteAsset(FixtureDirectory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void BuildsTimelineFromFixtureAndPreservesConfiguredClips()
        {
            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = GetFixturePath(),
                OutputDirectory = OutputDirectory,
                AssetName = "TimelineIntegration",
                ImportDirectory = FixtureDirectory + "/Imported"
            });

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True, string.Join("\n", result.Errors.Select(error => error.Message)));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.TimelineAssetPath, Is.EqualTo(TimelineAssetPath));
            Assert.That(result.PrefabPath, Is.EqualTo(PrefabAssetPath));

            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelineAssetPath);
            Assert.That(timeline, Is.Not.Null);

            var tracks = timeline.GetOutputTracks().ToArray();
            Assert.That(tracks, Has.Length.EqualTo(2));

            var music = tracks.Single(track => track is AudioTrack);
            Assert.That(music.name, Is.EqualTo("Music"));
            var audioClips = music.GetClips().ToArray();
            Assert.That(audioClips, Has.Length.EqualTo(2));
            Assert.That(audioClips[0].start, Is.EqualTo(0).Within(0.0001));
            Assert.That(audioClips[0].clipIn, Is.EqualTo(0.25).Within(0.0001));
            Assert.That(audioClips[0].duration, Is.EqualTo(2).Within(0.0001));
            Assert.That(audioClips[0].displayName, Is.EqualTo("Intro"));
            Assert.That(audioClips[1].start, Is.EqualTo(2).Within(0.0001));
            Assert.That(audioClips[1].duration, Is.EqualTo(1.5).Within(0.0001));
            Assert.That(((AudioPlayableAsset)audioClips[0].asset).clip,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AudioClip>(AudioAssetPath)));

            var character = tracks.Single(track => track is AnimationTrack);
            Assert.That(character.name, Is.EqualTo("Character"));
            var animationClip = character.GetClips().Single();
            Assert.That(animationClip.start, Is.EqualTo(1).Within(0.0001));
            Assert.That(animationClip.clipIn, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(animationClip.duration, Is.EqualTo(3).Within(0.0001));
            Assert.That(animationClip.displayName, Is.EqualTo("Walk"));
            Assert.That(((AnimationPlayableAsset)animationClip.asset).clip,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationAssetPath)));
        }

        private static string GetFixturePath()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Packages/com.hidano.unity-timeline-builder/Tests/Fixtures/timeline-integration.csv");
        }

        private static string GetProjectPath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
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
    }
}
