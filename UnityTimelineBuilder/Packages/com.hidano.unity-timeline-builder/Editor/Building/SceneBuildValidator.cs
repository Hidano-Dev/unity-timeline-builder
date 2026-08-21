using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor
{
    internal sealed class SceneBuildValidationResult
    {
        public TimelineAsset Timeline { get; }
        public IReadOnlyList<GameObject> PrefabAssets { get; }

        public SceneBuildValidationResult(TimelineAsset timeline, IReadOnlyList<GameObject> prefabAssets)
        {
            PrefabAssets = prefabAssets ?? throw new ArgumentNullException(nameof(prefabAssets));
            Timeline = timeline;
        }
    }

    /// <summary>Scene 行を Phase B に渡す前に解決・検証するバリデーター。</summary>
    internal sealed class SceneBuildValidator
    {
        public bool TryValidate(SceneBuildPlan plan, IReadOnlyList<ClipRow> clipRows,
            string generatedTimelinePath, string generatedPrefabPath, string outputDirectory,
            out SceneBuildValidationResult result, out IReadOnlyList<BuildError> errors)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (clipRows == null) throw new ArgumentNullException(nameof(clipRows));

            var validationErrors = new List<BuildError>();
            result = null;
            var timeline = ResolveTimeline(plan.Definition, generatedTimelinePath, validationErrors);
            var prefabs = ResolvePrefabs(plan.Prefabs, validationErrors);
            ValidateTrackNames(plan.Bindings, clipRows, timeline, plan.Definition, validationErrors);
            ValidateSceneOutputPath(plan.Definition, outputDirectory, generatedTimelinePath,
                generatedPrefabPath, validationErrors);

            errors = validationErrors;
            if (validationErrors.Count > 0 || prefabs.Any(prefab => prefab == null))
                return false;

            result = new SceneBuildValidationResult(timeline, prefabs);
            return true;
        }

        private static TimelineAsset ResolveTimeline(SceneDefinitionRow definition, string generatedPath,
            List<BuildError> errors)
        {
            var reference = definition.TimelineAssetPath;
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            if (!reference.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new BuildError(BuildErrorCode.SceneTimelineNotFound, definition.LineNumber, reference,
                    $"Scene TimelineAsset reference must be empty or an Assets/ path: '{reference}'."));
                return null;
            }

            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(reference);
            if (timeline == null)
                errors.Add(new BuildError(BuildErrorCode.SceneTimelineNotFound, definition.LineNumber, reference,
                    $"TimelineAsset was not found at '{reference}'."));
            return timeline;
        }

        private static List<GameObject> ResolvePrefabs(IReadOnlyList<ScenePrefabRow> rows,
            List<BuildError> errors)
        {
            var result = new List<GameObject>();
            foreach (var row in rows)
            {
                var path = row.PrefabAssetPath;
                GameObject prefab = null;
                if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                    errors.Add(new BuildError(BuildErrorCode.ScenePrefabInvalid, row.LineNumber, path,
                        $"Prefab asset was not found or is not a GameObject Prefab: '{path}'."));
                result.Add(prefab);
            }
            return result;
        }

        private static void ValidateTrackNames(IReadOnlyList<SceneBindRow> bindings,
            IReadOnlyList<ClipRow> clipRows, TimelineAsset explicitTimeline,
            SceneDefinitionRow definition, List<BuildError> errors)
        {
            var trackNames = new HashSet<string>(StringComparer.Ordinal);
            if (explicitTimeline != null)
            {
                foreach (var track in explicitTimeline.GetOutputTracks().OfType<AnimationTrack>())
                    trackNames.Add(track.name);
            }
            else
            {
                var hasCustomTrackType = clipRows.Any(row =>
                    !string.Equals(row.TrackType, "Animation", StringComparison.OrdinalIgnoreCase));
                if (hasCustomTrackType)
                    return;
                foreach (var row in clipRows.Where(row =>
                    string.Equals(row.TrackType, "Animation", StringComparison.OrdinalIgnoreCase)))
                    trackNames.Add(row.TrackName);
            }

            foreach (var binding in bindings)
            {
                if (!trackNames.Contains(binding.TrackName))
                    errors.Add(new BuildError(BuildErrorCode.BindTrackNotFound, binding.LineNumber,
                        binding.TrackName,
                        $"AnimationTrack '{binding.TrackName}' was not found in the TimelineAsset."));
            }
        }

        private static void ValidateSceneOutputPath(SceneDefinitionRow definition, string outputDirectory,
            string generatedTimelinePath, string generatedPrefabPath, List<BuildError> errors)
        {
            var scenePath = outputDirectory.Replace('\\', '/').TrimEnd('/') + "/" + definition.SceneName + ".unity";
            if (string.Equals(scenePath, generatedTimelinePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenePath, generatedPrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new BuildError(BuildErrorCode.RowValidationError, definition.LineNumber, scenePath,
                    $"Scene output path conflicts with another generated asset: '{scenePath}'."));
            }
        }
    }
}
