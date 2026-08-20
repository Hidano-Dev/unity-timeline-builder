using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor
{
    internal interface ITrackBuilder
    {
        string TrackTypeKey { get; }
        string ResourceKind { get; }
        TrackAsset CreateTrack(TimelineAsset timeline, string trackName);
        void AddClip(TrackAsset track, ClipRow row, Object resolvedAsset);
    }
}
