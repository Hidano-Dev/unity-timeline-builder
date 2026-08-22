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
        /// <summary>グループ成果物を格納するフォルダ（&lt;出力先&gt;/&lt;Scene名またはアセット名&gt;）。</summary>
        public string GroupDirectory { get; }
        public string TimelineAssetPath { get; }
        public string PrefabPath { get; }
        /// <summary>Scene計画がないグループではnull。</summary>
        public string ScenePath { get; }
        /// <summary>このグループの計画で発生したリネーム警告。</summary>
        public IReadOnlyList<string> Warnings { get; }

        public PlannedGroupOutputs(TimelineGroupPlan group, string assetName, string groupDirectory,
            string timelineAssetPath, string prefabPath, string scenePath,
            IReadOnlyList<string> warnings)
        {
            Group = group;
            AssetName = assetName;
            GroupDirectory = groupDirectory;
            TimelineAssetPath = timelineAssetPath;
            PrefabPath = prefabPath;
            ScenePath = scenePath;
            Warnings = new ReadOnlyCollection<string>(
                new List<string>(warnings ?? new string[0]));
        }
    }

    /// <summary>ビルド内の出力パスを計画し、名前の衝突を連番で解決する純ロジック。
    /// グループごとに Scene 名（Scene 計画がない場合はアセット名）のフォルダを割り当て、
    /// Scenes / Timelines / Prefabs のサブフォルダへ成果物を配置する。
    /// 衝突単位はフォルダ名（大文字小文字非区別）で、サフィックスはフォルダ名と
    /// その由来の名前（Scene 名またはアセット名）へ一体で適用される。</summary>
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
            var folderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                if (group == null)
                    throw new ArgumentException("Groups cannot contain null entries.", nameof(groups));

                var warnings = new List<string>();
                var assetName = group.TimelineName == null
                    ? (fallbackAssetName ?? string.Empty).Trim()
                    : group.TimelineName.Trim();
                string sceneName = null;
                string folderName;
                if (group.ScenePlan != null && group.ScenePlan.Definition != null)
                {
                    sceneName = ResolveFolderName(group.ScenePlan.Definition.SceneName.Trim(),
                        folderNames, outputDirectory, "Scene", warnings);
                    folderName = sceneName;
                }
                else
                {
                    assetName = ResolveFolderName(assetName, folderNames, outputDirectory,
                        "アセット", warnings);
                    folderName = assetName;
                }
                folderNames.Add(folderName);

                var groupDirectory = CombineAssetPath(outputDirectory, folderName);
                var timelinePath = groupDirectory + "/Timelines/" + assetName + ".playable";
                var prefabPath = groupDirectory + "/Prefabs/" + assetName + ".prefab";
                var scenePath = sceneName == null
                    ? null
                    : groupDirectory + "/Scenes/" + sceneName + ".unity";

                planned.Add(new PlannedGroupOutputs(group, assetName, groupDirectory,
                    timelinePath, prefabPath, scenePath, warnings));
            }

            return new ReadOnlyCollection<PlannedGroupOutputs>(planned);
        }

        private static string ResolveFolderName(string originalName, ISet<string> usedNames,
            string outputDirectory, string kind, ICollection<string> warnings)
        {
            var candidate = originalName;
            var suffix = 1;
            while (usedNames.Contains(candidate))
                candidate = originalName + " (" + suffix++ + ")";

            if (!string.Equals(candidate, originalName, StringComparison.Ordinal))
            {
                var finalPath = CombineAssetPath(outputDirectory, candidate);
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
