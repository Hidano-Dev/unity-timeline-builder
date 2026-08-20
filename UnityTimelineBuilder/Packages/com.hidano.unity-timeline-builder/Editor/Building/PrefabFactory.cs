using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>PlayableDirector 付き Prefab を生成・保存するファクトリ。</summary>
    internal sealed class PrefabFactory
    {
        /// <summary>PlayableDirector 付き Prefab を保存し、アセットパスを返す。</summary>
        /// <exception cref="BuildException">Prefab 保存に失敗した場合。</exception>
        public string Create(TimelineAsset timeline, string prefabPath, string gameObjectName)
        {
            if (timeline == null)
                throw new ArgumentNullException(nameof(timeline));
            if (string.IsNullOrWhiteSpace(prefabPath))
                throw new ArgumentException("Prefab asset path is required.", nameof(prefabPath));
            if (!prefabPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Prefab asset path must be under Assets/.", nameof(prefabPath));
            if (string.IsNullOrWhiteSpace(gameObjectName))
                throw new ArgumentException("GameObject name is required.", nameof(gameObjectName));

            var existingAsset = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            if (existingAsset != null && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                throw new BuildException($"Output asset is not a Prefab: '{prefabPath}'.");
            if (existingAsset != null)
                Debug.Log($"[UnityTimelineBuilder] Overwriting Prefab: {prefabPath}");

            var root = new GameObject(gameObjectName);
            try
            {
                var director = root.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                    throw new BuildException($"Failed to create Prefab at '{prefabPath}'.");

                AssetDatabase.SaveAssets();
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                    throw new BuildException($"Prefab was not created at '{prefabPath}'.");

                return prefabPath;
            }
            catch (BuildException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new BuildException($"Failed to create Prefab at '{prefabPath}'.", exception);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
