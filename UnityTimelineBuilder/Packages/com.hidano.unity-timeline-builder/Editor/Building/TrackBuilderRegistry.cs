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
            Register(new AnimationTrackBuilder());
        }
    }
}
