using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>SceneBind 行に従って AnimationTrack へ Animator を割り当てるコンポーネント。</summary>
    internal sealed class TrackBindingApplier
    {
        /// <summary>全バインド指定を検証・適用し、発生した全エラーを返す(空 = 成功)。</summary>
        public IReadOnlyList<BuildError> Apply(PlayableDirector director,
            TimelineAsset timeline,
            Scene scene,
            GameObject directorObject,
            IReadOnlyList<SceneBindRow> bindings)
        {
            if (director == null)
                throw new ArgumentNullException(nameof(director));
            if (timeline == null)
                throw new ArgumentNullException(nameof(timeline));
            if (!scene.IsValid())
                throw new ArgumentException("Scene must be valid.", nameof(scene));
            if (directorObject == null)
                throw new ArgumentNullException(nameof(directorObject));
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));

            var errors = new List<BuildError>();
            var tracks = timeline.GetOutputTracks().OfType<AnimationTrack>().ToArray();
            var targets = FindTargets(scene, directorObject);

            foreach (var binding in bindings)
            {
                if (binding == null)
                    continue;

                var matchingTracks = tracks
                    .Where(track => string.Equals(track.name, binding.TrackName, StringComparison.Ordinal))
                    .ToArray();
                if (matchingTracks.Length == 0)
                {
                    errors.Add(CreateError(BuildErrorCode.BindTrackNotFound, binding.LineNumber,
                        $"AnimationTrack '{binding.TrackName}' was not found."));
                    continue;
                }
                if (matchingTracks.Length > 1)
                {
                    errors.Add(CreateError(BuildErrorCode.BindTrackDuplicated, binding.LineNumber,
                        $"AnimationTrack '{binding.TrackName}' is duplicated ({matchingTracks.Length})."));
                    continue;
                }

                var matchingTargets = targets
                    .Where(gameObject => string.Equals(gameObject.name, binding.GameObjectName, StringComparison.Ordinal))
                    .ToArray();
                if (matchingTargets.Length == 0)
                {
                    errors.Add(CreateError(BuildErrorCode.BindTargetNotFound, binding.LineNumber,
                        $"GameObject '{binding.GameObjectName}' was not found."));
                    continue;
                }
                if (matchingTargets.Length > 1)
                {
                    errors.Add(CreateError(BuildErrorCode.BindTargetDuplicated, binding.LineNumber,
                        $"GameObject '{binding.GameObjectName}' is duplicated ({matchingTargets.Length})."));
                    continue;
                }

                var animator = matchingTargets[0].GetComponent<Animator>();
                if (animator == null)
                {
                    errors.Add(CreateError(BuildErrorCode.BindTargetMissingAnimator, binding.LineNumber,
                        $"GameObject '{binding.GameObjectName}' does not have an Animator component."));
                    continue;
                }

                director.SetGenericBinding(matchingTracks[0], animator);
            }

            return errors;
        }

        private static IReadOnlyList<GameObject> FindTargets(Scene scene, GameObject directorObject)
        {
            var targets = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == directorObject)
                    continue;

                targets.AddRange(root.transform
                    .GetComponentsInChildren<Transform>(true)
                    .Select(transform => transform.gameObject));
            }
            return targets;
        }

        private static BuildError CreateError(BuildErrorCode code, int lineNumber, string message)
        {
            return new BuildError(code, lineNumber, null, $"Line {lineNumber}: {message}");
        }
    }
}
