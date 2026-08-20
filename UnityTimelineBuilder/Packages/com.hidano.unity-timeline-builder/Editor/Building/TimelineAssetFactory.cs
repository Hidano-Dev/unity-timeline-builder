using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor
{
    internal sealed class BuildException : Exception
    {
        public BuildException(string message) : base(message)
        {
        }

        public BuildException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>リソース解決済みの 1 行と、その行を構築するビルダーを表す。</summary>
    internal sealed class ResolvedClipRow
    {
        public ClipRow Row { get; }
        public ITrackBuilder Builder { get; }
        public UnityEngine.Object Asset { get; }

        public ResolvedClipRow(ClipRow row, ITrackBuilder builder, UnityEngine.Object asset)
        {
            Row = row ?? throw new ArgumentNullException(nameof(row));
            Builder = builder ?? throw new ArgumentNullException(nameof(builder));
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }
    }

    /// <summary>解決済み行から TimelineAsset を生成・保存するファクトリ。</summary>
    internal sealed class TimelineAssetFactory
    {
        public TimelineAsset Create(IReadOnlyList<ResolvedClipRow> rows, string timelineAssetPath)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));
            if (string.IsNullOrWhiteSpace(timelineAssetPath))
                throw new ArgumentException("Timeline asset path is required.", nameof(timelineAssetPath));
            if (!timelineAssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Timeline asset path must be under Assets/.", nameof(timelineAssetPath));

            var timeline = LoadOrCreateTimeline(timelineAssetPath);
            try
            {
                DeleteExistingTracks(timeline);

                var tracks = new Dictionary<TrackKey, TrackAsset>();
                foreach (var resolvedRow in rows)
                {
                    if (resolvedRow == null)
                        throw new BuildException("Resolved clip row cannot be null.");

                    var row = resolvedRow.Row;
                    var key = new TrackKey(row.TrackType, row.TrackName);
                    if (!tracks.TryGetValue(key, out var track))
                    {
                        track = resolvedRow.Builder.CreateTrack(timeline, row.TrackName);
                        if (track == null)
                            throw new BuildException($"Track builder returned null for '{row.TrackType}'.");
                        tracks.Add(key, track);
                    }

                    resolvedRow.Builder.AddClip(track, row, resolvedRow.Asset);
                }

                AssetDatabase.SaveAssets();
                return timeline;
            }
            catch (BuildException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new BuildException($"Failed to build TimelineAsset at '{timelineAssetPath}'.", exception);
            }
        }

        private static TimelineAsset LoadOrCreateTimeline(string timelineAssetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelineAssetPath);
            if (existing != null)
            {
                Debug.Log($"[UnityTimelineBuilder] Overwriting TimelineAsset: {timelineAssetPath}");
                return existing;
            }

            if (AssetDatabase.LoadMainAssetAtPath(timelineAssetPath) != null)
                throw new BuildException($"Output asset is not a TimelineAsset: '{timelineAssetPath}'.");

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = System.IO.Path.GetFileNameWithoutExtension(timelineAssetPath);
            try
            {
                AssetDatabase.CreateAsset(timeline, timelineAssetPath);
                return timeline;
            }
            catch (Exception exception)
            {
                UnityEngine.Object.DestroyImmediate(timeline);
                throw new BuildException($"Failed to create TimelineAsset at '{timelineAssetPath}'.", exception);
            }
        }

        private static void DeleteExistingTracks(TimelineAsset timeline)
        {
            foreach (var track in timeline.GetOutputTracks().ToArray())
            {
                if (!timeline.DeleteTrack(track))
                    throw new BuildException($"Failed to delete existing track '{track.name}'.");
            }
        }

        private struct TrackKey : IEquatable<TrackKey>
        {
            private readonly string trackType;
            private readonly string trackName;

            public TrackKey(string trackType, string trackName)
            {
                this.trackType = (trackType ?? string.Empty).Trim();
                this.trackName = trackName ?? string.Empty;
            }

            public bool Equals(TrackKey other)
            {
                return string.Equals(trackType, other.trackType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(trackName, other.trackName, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TrackKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((StringComparer.OrdinalIgnoreCase.GetHashCode(trackType) * 397)
                        ^ StringComparer.Ordinal.GetHashCode(trackName));
                }
            }
        }
    }
}
