using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>Scene 定義行を表す不変モデル。</summary>
    internal sealed class SceneDefinitionRow
    {
        public int LineNumber { get; }
        public string SceneName { get; }
        public string TimelineAssetPath { get; }

        public SceneDefinitionRow(int lineNumber, string sceneName, string timelineAssetPath)
        {
            LineNumber = lineNumber;
            SceneName = sceneName;
            TimelineAssetPath = timelineAssetPath;
        }
    }

    /// <summary>Scene に配置する Prefab 行を表す不変モデル。</summary>
    internal sealed class ScenePrefabRow
    {
        public int LineNumber { get; }
        public string PrefabAssetPath { get; }

        public ScenePrefabRow(int lineNumber, string prefabAssetPath)
        {
            LineNumber = lineNumber;
            PrefabAssetPath = prefabAssetPath;
        }
    }

    /// <summary>AnimationTrack と GameObject のバインド指定行を表す不変モデル。</summary>
    internal sealed class SceneBindRow
    {
        public int LineNumber { get; }
        public string TrackName { get; }
        public string GameObjectName { get; }

        public SceneBindRow(int lineNumber, string trackName, string gameObjectName)
        {
            LineNumber = lineNumber;
            TrackName = trackName;
            GameObjectName = gameObjectName;
        }
    }

    /// <summary>パース済み Scene 構築計画を表す不変モデル。</summary>
    internal sealed class SceneBuildPlan
    {
        public SceneDefinitionRow Definition { get; }
        public IReadOnlyList<ScenePrefabRow> Prefabs { get; }
        public IReadOnlyList<SceneBindRow> Bindings { get; }

        public SceneBuildPlan(SceneDefinitionRow definition,
            IReadOnlyList<ScenePrefabRow> prefabs,
            IReadOnlyList<SceneBindRow> bindings)
        {
            Definition = definition;
            Prefabs = Copy(prefabs);
            Bindings = Copy(bindings);
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> rows)
        {
            return new ReadOnlyCollection<T>(
                new List<T>(rows ?? new T[0]));
        }
    }
}
