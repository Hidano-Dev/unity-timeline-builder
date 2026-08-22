using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>構築処理の成否、生成物のパス、発生したエラーを表す結果。</summary>
    public sealed class BuildResult
    {
        public bool Success { get; }
        public string TimelineAssetPath { get; }
        public string PrefabPath { get; }
        public string ScenePath { get; }
        public IReadOnlyList<BuildOutput> Outputs { get; }
        public IReadOnlyList<BuildError> Errors { get; }

        public BuildResult(bool success, string timelineAssetPath, string prefabPath,
            IReadOnlyList<BuildError> errors)
            : this(success, timelineAssetPath, prefabPath, null, errors)
        {
        }

        public BuildResult(bool success, string timelineAssetPath, string prefabPath, string scenePath,
            IReadOnlyList<BuildError> errors)
            : this(success, CreateLegacyOutputs(timelineAssetPath, prefabPath, scenePath), errors)
        {
        }

        public BuildResult(bool success, IReadOnlyList<BuildOutput> outputs,
            IReadOnlyList<BuildError> errors)
        {
            Success = success;
            Outputs = new ReadOnlyCollection<BuildOutput>(
                new List<BuildOutput>(outputs ?? new BuildOutput[0]));
            var first = Outputs.Count == 0 ? null : Outputs[0];
            TimelineAssetPath = first == null ? null : first.TimelineAssetPath;
            PrefabPath = first == null ? null : first.PrefabPath;
            ScenePath = first == null ? null : first.ScenePath;
            Errors = new ReadOnlyCollection<BuildError>(
                new List<BuildError>(errors ?? new BuildError[0]));
        }

        private static IReadOnlyList<BuildOutput> CreateLegacyOutputs(
            string timelineAssetPath, string prefabPath, string scenePath)
        {
            if (timelineAssetPath == null && prefabPath == null && scenePath == null)
                return new BuildOutput[0];

            var assetName = timelineAssetPath == null
                ? null
                : Path.GetFileNameWithoutExtension(timelineAssetPath);
            return new[]
            {
                new BuildOutput(assetName, assetName, timelineAssetPath, prefabPath, scenePath, scenePath != null)
            };
        }
    }
}
