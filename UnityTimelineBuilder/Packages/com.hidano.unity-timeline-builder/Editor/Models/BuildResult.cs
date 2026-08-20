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
        public IReadOnlyList<BuildError> Errors { get; }

        public BuildResult(bool success, string timelineAssetPath, string prefabPath,
            IReadOnlyList<BuildError> errors)
        {
            Success = success;
            TimelineAssetPath = timelineAssetPath;
            PrefabPath = prefabPath;
            Errors = new ReadOnlyCollection<BuildError>(
                new List<BuildError>(errors ?? new BuildError[0]));
        }
    }
}
