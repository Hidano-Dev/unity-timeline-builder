using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class TimelineAssetFactoryTests
    {
        private const string AssetPath = "Assets/TimelineAssetFactoryTests.playable";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(AssetPath);
            AssetDatabase.SaveAssets();
        }

        [Test]
        public void GroupsRowsByTrackTypeAndExactTrackName()
        {
            var audioA = AudioClip.Create("AudioA", 1000, 1, 1000, false);
            var audioB = AudioClip.Create("AudioB", 1000, 1, 1000, false);
            var animation = new AnimationClip { name = "Animation" };
            try
            {
                var factory = new TimelineAssetFactory();
                var rows = new[]
                {
                    new ResolvedClipRow(new ClipRow(2, " audio ", "Shared", "First", 0, 0, 1, "a.wav"), new AudioTrackBuilder(), audioA),
                    new ResolvedClipRow(new ClipRow(3, "Audio", "Shared", "Second", 1, 0.25, 2, "b.wav"), new AudioTrackBuilder(), audioB),
                    new ResolvedClipRow(new ClipRow(4, "Animation", "SharedAnimation", "Walk", 2, 0, 3, "walk.anim"), new AnimationTrackBuilder(), animation),
                    new ResolvedClipRow(new ClipRow(5, "Audio", "Other", "Third", 3, 0, 1, "a.wav"), new AudioTrackBuilder(), audioA)
                };

                var timeline = factory.Create(rows, AssetPath);
                var tracks = timeline.GetOutputTracks().ToArray();

                Assert.That(tracks, Has.Length.EqualTo(3));
                Assert.That(tracks.Count(track => track.name == "Shared" && track is AudioTrack), Is.EqualTo(1));
                Assert.That(tracks.Count(track => track.name == "SharedAnimation" && track is AnimationTrack), Is.EqualTo(1));
                Assert.That(tracks.Single(track => track.name == "Shared" && track is AudioTrack).GetClips().Count(), Is.EqualTo(2));
                Assert.That(tracks.Sum(track => track.GetClips().Count()), Is.EqualTo(4));

                var sharedAudio = tracks.Single(track => track.name == "Shared" && track is AudioTrack);
                var clips = sharedAudio.GetClips().ToArray();
                Assert.That(clips[0].start, Is.EqualTo(0));
                Assert.That(clips[1].duration, Is.EqualTo(2));
                Assert.That(clips[1].displayName, Is.EqualTo("Second"));
                Assert.That(((AudioPlayableAsset)clips[1].asset).clip, Is.SameAs(audioB));
            }
            finally
            {
                Object.DestroyImmediate(audioA);
                Object.DestroyImmediate(audioB);
                Object.DestroyImmediate(animation);
            }
        }

        [Test]
        public void OverwritesExistingAssetInPlaceAndPreservesGuid()
        {
            var oldAudio = AudioClip.Create("OldAudio", 1000, 1, 1000, false);
            var newAudio = AudioClip.Create("NewAudio", 1000, 1, 1000, false);
            try
            {
                var factory = new TimelineAssetFactory();
                var first = factory.Create(
                    new[] { new ResolvedClipRow(new ClipRow(2, "Audio", "Old", "OldClip", 0, 0, 1, "old.wav"), new AudioTrackBuilder(), oldAudio) },
                    AssetPath);
                var guid = AssetDatabase.AssetPathToGUID(AssetPath);

                var second = factory.Create(
                    new[] { new ResolvedClipRow(new ClipRow(2, "Audio", "New", "NewClip", 2, 0, 3, "new.wav"), new AudioTrackBuilder(), newAudio) },
                    AssetPath);

                Assert.That(second, Is.SameAs(first));
                Assert.That(AssetDatabase.AssetPathToGUID(AssetPath), Is.EqualTo(guid));
                Assert.That(second.GetOutputTracks().Count(), Is.EqualTo(1));
                Assert.That(second.GetOutputTracks().Single().name, Is.EqualTo("New"));
                Assert.That(second.GetOutputTracks().Single().GetClips().Count(), Is.EqualTo(1));
                Assert.That(second.GetOutputTracks().Single().GetClips().Single().displayName, Is.EqualTo("NewClip"));
                Assert.That(File.Exists(Path.Combine(Application.dataPath, "TimelineAssetFactoryTests.playable")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(oldAudio);
                Object.DestroyImmediate(newAudio);
            }
        }
    }
}
