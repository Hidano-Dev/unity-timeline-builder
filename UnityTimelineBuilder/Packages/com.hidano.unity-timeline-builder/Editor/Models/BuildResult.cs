using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>構築処理の成否、生成物のパス、発生したエラーを表す結果。</summary>
    public sealed class BuildResult
    {
        public bool Success { get; }
        public string TimelineAssetPath { get; }
        public string PrefabPath { get; }
        public string ScenePath { get; }
        public IReadOnlyList<BuildError> Errors { get; }

        public BuildResult(bool success, string timelineAssetPath, string prefabPath,
            IReadOnlyList<BuildError> errors)
            : this(success, timelineAssetPath, prefabPath, null, errors)
        {
        }

        public BuildResult(bool success, string timelineAssetPath, string prefabPath, string scenePath,
            IReadOnlyList<BuildError> errors)
        {
            Success = success;
            TimelineAssetPath = timelineAssetPath;
            PrefabPath = prefabPath;
            ScenePath = scenePath;
            Errors = new ReadOnlyCollection<BuildError>(
                new List<BuildError>(errors ?? new BuildError[0]));
        }
    }
}
