using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class AnimationTrackBuilderTests
    {
        [Test]
        public void CreatesAnimationTrackWithRequestedName()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            try
            {
                var builder = new AnimationTrackBuilder();

                var track = builder.CreateTrack(timeline, "Character");

                Assert.That(track, Is.TypeOf<AnimationTrack>());
                Assert.That(track.name, Is.EqualTo("Character"));
            }
            finally
            {
                Object.DestroyImmediate(timeline);
            }
        }

        [Test]
        public void AddsAnimationClipWithRowValuesAndResolvedAsset()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var animation = new AnimationClip();
            animation.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            try
            {
                var builder = new AnimationTrackBuilder();
                var track = builder.CreateTrack(timeline, "Character");
                var row = new ClipRow(2, "Animation", "Character", "Walk", 1.5, 0.25, 2.0, "walk.anim");

                builder.AddClip(track, row, animation);

                var clips = track.GetClips().ToArray();
                Assert.That(clips, Has.Length.EqualTo(1));
                Assert.That(clips[0].start, Is.EqualTo(1.5));
                Assert.That(clips[0].duration, Is.EqualTo(2.0));
                Assert.That(clips[0].displayName, Is.EqualTo("Walk"));
                Assert.That(((AnimationPlayableAsset)clips[0].asset).clip, Is.SameAs(animation));
                Assert.That(track.isSubTrack, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(animation);
                Object.DestroyImmediate(timeline);
            }
        }

        [Test]
        public void FallsBackToAssetLengthAndNameWhenOptionalValuesAreEmpty()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var animation = new AnimationClip { name = "Walk" };
            animation.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 1f));
            try
            {
                var builder = new AnimationTrackBuilder();
                var track = builder.CreateTrack(timeline, "Character");
                var row = new ClipRow(2, "Animation", "Character", "", 1.0, 0, null, "walk.anim");

                builder.AddClip(track, row, animation);

                var clips = track.GetClips().ToArray();
                Assert.That(clips, Has.Length.EqualTo(1));
                Assert.That(clips[0].duration, Is.EqualTo(animation.length).Within(0.0001));
                Assert.That(clips[0].displayName, Is.EqualTo("Walk"));
            }
            finally
            {
                Object.DestroyImmediate(animation);
                Object.DestroyImmediate(timeline);
            }
        }
    }
}
