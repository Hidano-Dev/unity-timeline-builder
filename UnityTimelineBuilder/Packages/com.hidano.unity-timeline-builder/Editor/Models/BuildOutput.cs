namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>1 つの Timeline グループに対応する生成物の結果。</summary>
    public sealed class BuildOutput
    {
        /// <summary>CSV 記載の Timeline 名。レガシー入力では確定アセット名と同値。</summary>
        public string TimelineName { get; }
        /// <summary>衝突解決後の確定アセット名。</summary>
        public string ResolvedAssetName { get; }
        public string TimelineAssetPath { get; }
        public string PrefabPath { get; }
        public string ScenePath { get; }
        /// <summary>ScenePath が未生成なのか Scene 計画なしなのかを区別する。</summary>
        public bool HasScenePlan { get; }

        public BuildOutput(string timelineName, string resolvedAssetName,
            string timelineAssetPath, string prefabPath, string scenePath, bool hasScenePlan)
        {
            TimelineName = timelineName;
            ResolvedAssetName = resolvedAssetName;
            TimelineAssetPath = timelineAssetPath;
            PrefabPath = prefabPath;
            ScenePath = scenePath;
            HasScenePlan = hasScenePlan;
        }
    }
}
