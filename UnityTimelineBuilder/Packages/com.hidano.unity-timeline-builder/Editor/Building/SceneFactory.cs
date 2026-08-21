using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>Phase A で解決済みの Scene 構築コンテキスト。</summary>
    internal sealed class SceneBuildContext
    {
        public SceneBuildPlan Plan { get; }
        public TimelineAsset Timeline { get; }
        public IReadOnlyList<GameObject> PrefabAssets { get; }
        public string ScenePath { get; }
        public string DirectorObjectName { get; }
        public string TimelineAssetPath { get; }

        public SceneBuildContext(SceneBuildPlan plan, TimelineAsset timeline,
            IReadOnlyList<GameObject> prefabAssets, string scenePath, string directorObjectName,
            string timelineAssetPath)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            if (prefabAssets == null)
                throw new ArgumentNullException(nameof(prefabAssets));
            if (string.IsNullOrWhiteSpace(scenePath))
                throw new ArgumentException("Scene path is required.", nameof(scenePath));
            if (string.IsNullOrWhiteSpace(directorObjectName))
                throw new ArgumentException("Director object name is required.", nameof(directorObjectName));
            if (string.IsNullOrWhiteSpace(timelineAssetPath))
                throw new ArgumentException("Timeline asset path is required.", nameof(timelineAssetPath));
            if (prefabAssets.Any(asset => asset == null))
                throw new ArgumentException("Prefab assets cannot contain null.", nameof(prefabAssets));

            PrefabAssets = new ReadOnlyCollection<GameObject>(new List<GameObject>(prefabAssets));
            ScenePath = scenePath;
            DirectorObjectName = directorObjectName;
            TimelineAssetPath = timelineAssetPath;
        }
    }

    /// <summary>空 Scene の生成、配置、バインド適用、保存を行うファクトリ。</summary>
    internal sealed class SceneFactory
    {
        public bool TryCreate(SceneBuildContext context, out string scenePath,
            out IReadOnlyList<BuildError> errors)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            scenePath = null;
            var resultErrors = new List<BuildError>();
            errors = resultErrors;

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                resultErrors.Add(new BuildError(BuildErrorCode.SceneBuildCanceled, null, context.ScenePath,
                    "Scene build was canceled because modified scenes were not saved."));
                return false;
            }

            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || scene.GetRootGameObjects().Length > 0)
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // NewScene(Single) は旧シーンのアンロード時に、マネージド参照しか残っていない
                // アセットを破棄することがある。破棄済み参照は C# の null ではないため、
                // UnityEngine.Object の null 判定でパスから再ロードする。
                var timeline = context.Timeline;
                if (timeline == null)
                    timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(context.TimelineAssetPath);
                if (timeline == null)
                {
                    resultErrors.Add(new BuildError(BuildErrorCode.SceneTimelineNotFound, null,
                        context.TimelineAssetPath,
                        $"TimelineAsset was not available at '{context.TimelineAssetPath}'."));
                    return false;
                }
                var directorObject = new GameObject(context.DirectorObjectName);
                var director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;

                for (var index = 0; index < context.PrefabAssets.Count; index++)
                {
                    var prefab = context.PrefabAssets[index];
                    if (prefab == null)
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                            context.Plan.Prefabs[index].PrefabAssetPath);
                    if (prefab == null || PrefabUtility.InstantiatePrefab(prefab, scene) == null)
                    {
                        resultErrors.Add(new BuildError(BuildErrorCode.ScenePrefabInvalid,
                            context.Plan.Prefabs[index].LineNumber, context.Plan.Prefabs[index].PrefabAssetPath,
                            $"Failed to instantiate Prefab '{context.Plan.Prefabs[index].PrefabAssetPath}'."));
                    }
                }

                if (resultErrors.Count == 0)
                    resultErrors.AddRange(new TrackBindingApplier().Apply(director, timeline,
                        scene, directorObject, context.Plan.Bindings));

                if (resultErrors.Count > 0)
                    return false;

                if (File.Exists(ProjectPath(context.ScenePath)))
                    Debug.Log($"[UnityTimelineBuilder] Overwriting Scene: {context.ScenePath}");

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, context.ScenePath))
                {
                    resultErrors.Add(new BuildError(BuildErrorCode.SceneWriteFailed, null, context.ScenePath,
                        $"Failed to save Scene at '{context.ScenePath}'."));
                    return false;
                }

                scenePath = context.ScenePath;
                return true;
            }
            catch (Exception exception)
            {
                resultErrors.Add(new BuildError(BuildErrorCode.SceneWriteFailed, null, context.ScenePath,
                    $"Failed to build Scene at '{context.ScenePath}': {exception.Message}"));
                return false;
            }
        }

        private static string ProjectPath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
