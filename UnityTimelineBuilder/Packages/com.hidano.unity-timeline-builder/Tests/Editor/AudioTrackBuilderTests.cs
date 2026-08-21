using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class AudioTrackBuilderTests
    {
        [Test]
        public void FallsBackToAssetLengthAndNameWhenOptionalValuesAreEmpty()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var audioClip = AudioClip.Create("Intro", 48000 * 2, 1, 48000, false);
            try
            {
                var builder = new AudioTrackBuilder();
                var track = builder.CreateTrack(timeline, "BGM");
                var row = new ClipRow(2, "Audio", "BGM", "", 0.5, 0, null, "intro.wav");

                builder.AddClip(track, row, audioClip);

                var clips = track.GetClips().ToArray();
                Assert.That(clips, Has.Length.EqualTo(1));
                Assert.That(clips[0].duration, Is.EqualTo(audioClip.length).Within(0.0001));
                Assert.That(clips[0].displayName, Is.EqualTo("Intro"));
            }
            finally
            {
                Object.DestroyImmediate(audioClip);
                Object.DestroyImmediate(timeline);
            }
        }

        [Test]
        public void UsesRowValuesWhenProvided()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var audioClip = AudioClip.Create("Intro", 48000 * 2, 1, 48000, false);
            try
            {
                var builder = new AudioTrackBuilder();
                var track = builder.CreateTrack(timeline, "BGM");
                var row = new ClipRow(2, "Audio", "BGM", "CustomName", 0.5, 0.25, 1.5, "intro.wav");

                builder.AddClip(track, row, audioClip);

                var clips = track.GetClips().ToArray();
                Assert.That(clips, Has.Length.EqualTo(1));
                Assert.That(clips[0].duration, Is.EqualTo(1.5).Within(0.0001));
                Assert.That(clips[0].displayName, Is.EqualTo("CustomName"));
            }
            finally
            {
                Object.DestroyImmediate(audioClip);
                Object.DestroyImmediate(timeline);
            }
        }
    }
}
