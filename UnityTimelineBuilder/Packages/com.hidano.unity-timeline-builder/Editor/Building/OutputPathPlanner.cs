using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>1グループ分の衝突解決済み出力パスを表す不変計画。</summary>
    internal sealed class PlannedGroupOutputs
    {
        public TimelineGroupPlan Group { get; }
        /// <summary>サフィックス適用後の最終アセット名。</summary>
        public string AssetName { get; }
        public string TimelineAssetPath { get; }
        public string PrefabPath { get; }
        /// <summary>Scene計画がないグループではnull。</summary>
        public string ScenePath { get; }
        /// <summary>このグループの計画で発生したリネーム警告。</summary>
        public IReadOnlyList<string> Warnings { get; }

        public PlannedGroupOutputs(TimelineGroupPlan group, string assetName,
            string timelineAssetPath, string prefabPath, string scenePath,
            IReadOnlyList<string> warnings)
        {
            Group = group;
            AssetName = assetName;
            TimelineAssetPath = timelineAssetPath;
            PrefabPath = prefabPath;
            ScenePath = scenePath;
            Warnings = new ReadOnlyCollection<string>(
                new List<string>(warnings ?? new string[0]));
        }
    }

    /// <summary>ビルド内の出力パスを計画し、名前の衝突を連番で解決する純ロジック。</summary>
    internal sealed class OutputPathPlanner
    {
        public IReadOnlyList<PlannedGroupOutputs> Plan(
            IReadOnlyList<TimelineGroupPlan> groups,
            string outputDirectory,
            string fallbackAssetName)
        {
            if (groups == null)
                throw new ArgumentNullException(nameof(groups));

            var planned = new List<PlannedGroupOutputs>(groups.Count);
            var assetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sceneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                if (group == null)
                    throw new ArgumentException("Groups cannot contain null entries.", nameof(groups));

                var warnings = new List<string>();
                var originalAssetName = group.TimelineName == null
                    ? (fallbackAssetName ?? string.Empty).Trim()
                    : group.TimelineName.Trim();
                var assetName = ResolveName(originalAssetName, assetNames, outputDirectory,
                    ".playable", "アセット", warnings);
                assetNames.Add(assetName);

                var timelinePath = CombineAssetPath(outputDirectory, assetName + ".playable");
                var prefabPath = CombineAssetPath(outputDirectory, assetName + ".prefab");
                string scenePath = null;
                if (group.ScenePlan != null && group.ScenePlan.Definition != null)
                {
                    var originalSceneName = group.ScenePlan.Definition.SceneName.Trim();
                    var sceneName = ResolveName(originalSceneName, sceneNames, outputDirectory,
                        ".unity", "Scene", warnings);
                    sceneNames.Add(sceneName);
                    scenePath = CombineAssetPath(outputDirectory, sceneName + ".unity");
                }

                planned.Add(new PlannedGroupOutputs(group, assetName, timelinePath, prefabPath,
                    scenePath, warnings));
            }

            return new ReadOnlyCollection<PlannedGroupOutputs>(planned);
        }

        private static string ResolveName(string originalName, ISet<string> usedNames,
            string outputDirectory, string extension, string kind, ICollection<string> warnings)
        {
            var candidate = originalName;
            var suffix = 1;
            while (usedNames.Contains(candidate))
                candidate = originalName + " (" + suffix++ + ")";

            if (!string.Equals(candidate, originalName, StringComparison.Ordinal))
            {
                var finalPath = CombineAssetPath(outputDirectory, candidate + extension);
                warnings.Add($"{kind} output renamed: '{originalName}' -> '{finalPath}'.");
            }

            return candidate;
        }

        private static string CombineAssetPath(string directory, string fileName)
        {
            return (directory ?? string.Empty).Replace('\\', '/').TrimEnd('/') + "/" + fileName;
        }
    }
}
