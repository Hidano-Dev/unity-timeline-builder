using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>AnimationTrack と AnimationPlayableAsset クリップを構築するビルダーです。</summary>
    internal sealed class AnimationTrackBuilder : ITrackBuilder
    {
        public string TrackTypeKey => "Animation";
        public string ResourceKind => "Animation";

        public TrackAsset CreateTrack(TimelineAsset timeline, string trackName)
        {
            if (timeline == null)
                throw new ArgumentNullException(nameof(timeline));
            if (string.IsNullOrWhiteSpace(trackName))
                throw new ArgumentException("Track name is required.", nameof(trackName));

            return timeline.CreateTrack<AnimationTrack>(null, trackName);
        }

        public void AddClip(TrackAsset track, ClipRow row, UnityEngine.Object resolvedAsset)
        {
            if (track == null)
                throw new ArgumentNullException(nameof(track));
            if (row == null)
                throw new ArgumentNullException(nameof(row));

            var animationClip = resolvedAsset as AnimationClip;
            if (animationClip == null)
                throw new ArgumentException("The resolved asset must be an AnimationClip.", nameof(resolvedAsset));

            var animationTrack = track as AnimationTrack;
            if (animationTrack == null)
                throw new ArgumentException("The track must be an AnimationTrack.", nameof(track));

            var timelineClip = animationTrack.CreateClip<AnimationPlayableAsset>();
            var playableAsset = timelineClip.asset as AnimationPlayableAsset;
            if (playableAsset == null)
                throw new InvalidOperationException("AnimationTrack created a non-animation playable asset.");
            playableAsset.clip = animationClip;
            timelineClip.start = row.StartTime;
            timelineClip.clipIn = row.ClipIn;
            timelineClip.duration = row.Duration;
            timelineClip.displayName = row.ClipName;
        }
    }
}
