using System;
using System.Collections.Generic;

namespace Hidano.UnityTimelineBuilder.Editor
{
    internal static class TrackBuilderRegistry
    {
        private static readonly Dictionary<string, ITrackBuilder> Builders =
            new Dictionary<string, ITrackBuilder>(StringComparer.OrdinalIgnoreCase);

        static TrackBuilderRegistry()
        {
            RegisterBuiltIns();
        }

        public static void Register(ITrackBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (string.IsNullOrWhiteSpace(builder.TrackTypeKey))
                throw new ArgumentException("Track builder type key is required.", nameof(builder));
            if (string.IsNullOrWhiteSpace(builder.ResourceKind))
                throw new ArgumentException("Track builder resource kind is required.", nameof(builder));

            Builders[builder.TrackTypeKey.Trim()] = builder;
        }

        public static bool TryGet(string trackTypeKey, out ITrackBuilder builder)
        {
            builder = null;
            return !string.IsNullOrWhiteSpace(trackTypeKey)
                && Builders.TryGetValue(trackTypeKey.Trim(), out builder);
        }

        public static bool IsKnownTrackType(string trackTypeKey)
        {
            return TryGet(trackTypeKey, out _);
        }

        internal static void ResetForTest()
        {
            Builders.Clear();
            RegisterBuiltIns();
        }

        private static void RegisterBuiltIns()
        {
            Register(new AudioTrackBuilder());
            Register(new BuiltInTrackBuilder("Animation", "Animation"));
        }

        // Concrete builders are supplied by the subsequent building tasks. Keeping the
        // built-in contracts registered here lets parsing and extension code use the
        // registry before those strategies are introduced.
        private sealed class BuiltInTrackBuilder : ITrackBuilder
        {
            public string TrackTypeKey { get; }
            public string ResourceKind { get; }

            public BuiltInTrackBuilder(string trackTypeKey, string resourceKind)
            {
                TrackTypeKey = trackTypeKey;
                ResourceKind = resourceKind;
            }

            public UnityEngine.Timeline.TrackAsset CreateTrack(
                UnityEngine.Timeline.TimelineAsset timeline, string trackName)
            {
                throw new NotSupportedException("The concrete track builder is not registered yet.");
            }

            public void AddClip(UnityEngine.Timeline.TrackAsset track, ClipRow row,
                UnityEngine.Object resolvedAsset)
            {
                throw new NotSupportedException("The concrete track builder is not registered yet.");
            }
        }
    }
}
