using System;
using System.Collections.Generic;

namespace Hidano.UnityTimelineBuilder.Editor
{
    internal static class TrackBuilderRegistry
    {
        private static readonly Dictionary<string, ITrackBuilder> Builders =
            new Dictionary<string, ITrackBuilder>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ReservedTrackTypeKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Scene",
                "ScenePrefab",
                "SceneBind"
            };

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

            var normalizedKey = builder.TrackTypeKey.Trim();
            if (ReservedTrackTypeKeys.Contains(normalizedKey))
                throw new ArgumentException("Track builder type key is reserved for Scene rows.", nameof(builder));

            Builders[normalizedKey] = builder;
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
