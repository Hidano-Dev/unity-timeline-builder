using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>AudioTrack と AudioPlayableAsset クリップを構築するビルダー。</summary>
    internal sealed class AudioTrackBuilder : ITrackBuilder
    {
        public string TrackTypeKey => "Audio";
        public string ResourceKind => "Audio";

        public TrackAsset CreateTrack(TimelineAsset timeline, string trackName)
        {
            if (timeline == null)
                throw new ArgumentNullException(nameof(timeline));
            if (string.IsNullOrWhiteSpace(trackName))
                throw new ArgumentException("Track name is required.", nameof(trackName));

            return timeline.CreateTrack<AudioTrack>(null, trackName);
        }

        public void AddClip(TrackAsset track, ClipRow row, UnityEngine.Object resolvedAsset)
        {
            if (track == null)
                throw new ArgumentNullException(nameof(track));
            if (row == null)
                throw new ArgumentNullException(nameof(row));

            var audioClip = resolvedAsset as AudioClip;
            if (audioClip == null)
                throw new ArgumentException("The resolved asset must be an AudioClip.", nameof(resolvedAsset));

            var audioTrack = track as AudioTrack;
            if (audioTrack == null)
                throw new ArgumentException("The track must be an AudioTrack.", nameof(track));

            var timelineClip = audioTrack.CreateClip<AudioPlayableAsset>();
            timelineClip.start = row.StartTime;
            timelineClip.clipIn = row.ClipIn;
            timelineClip.duration = row.Duration ?? audioClip.length;
            timelineClip.displayName = string.IsNullOrWhiteSpace(row.ClipName) ? audioClip.name : row.ClipName;
            var playableAsset = timelineClip.asset as AudioPlayableAsset;
            if (playableAsset == null)
                throw new InvalidOperationException("AudioTrack created a non-audio playable asset.");
            playableAsset.clip = audioClip;
        }
    }
}
