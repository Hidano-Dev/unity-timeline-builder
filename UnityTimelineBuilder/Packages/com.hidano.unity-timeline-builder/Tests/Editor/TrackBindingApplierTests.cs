using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class TrackBindingApplierTests
    {
        private Scene scene;
        private GameObject directorObject;
        private PlayableDirector director;
        private TimelineAsset timeline;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            directorObject = new GameObject("Director");
            director = directorObject.AddComponent<PlayableDirector>();
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            director.playableAsset = timeline;
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
            Object.DestroyImmediate(timeline);
        }

        [Test]
        public void AppliesAnimatorByOrdinalNameIncludingInactiveObjects()
        {
            var target = new GameObject("Character");
            target.SetActive(false);
            var animator = target.AddComponent<Animator>();
            var track = timeline.CreateTrack<AnimationTrack>(null, "Move");

            var errors = new TrackBindingApplier().Apply(director, timeline, scene, directorObject,
                new[] { new SceneBindRow(8, "Move", "Character") });

            Assert.That(errors, Is.Empty);
            Assert.That(director.GetGenericBinding(track), Is.SameAs(animator));
        }

        [Test]
        public void DoesNotTouchUnboundTracks()
        {
            var target = new GameObject("Character");
            var unbound = timeline.CreateTrack<AnimationTrack>(null, "Idle");
            timeline.CreateTrack<AnimationTrack>(null, "Move");

            var errors = new TrackBindingApplier().Apply(director, timeline, scene, directorObject,
                new[] { new SceneBindRow(8, "Move", "Missing") });

            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(director.GetGenericBinding(unbound), Is.Null);
        }

        [Test]
        public void CollectsAllBindingErrorsWithLineNumbers()
        {
            var noAnimator = new GameObject("NoAnimator");
            var duplicateA = new GameObject("Duplicate");
            var duplicateB = new GameObject("Duplicate");
            timeline.CreateTrack<AnimationTrack>(null, "ValidTrack");

            var errors = new TrackBindingApplier().Apply(director, timeline, scene, directorObject,
                new[]
                {
                    new SceneBindRow(10, "MissingTrack", "NoAnimator"),
                    new SceneBindRow(11, "ValidTrack", "NoAnimator"),
                    new SceneBindRow(12, "ValidTrack", "Duplicate"),
                    new SceneBindRow(13, "ValidTrack", "MissingObject")
                });

            Assert.That(errors.Select(error => error.Code), Is.EquivalentTo(new[]
            {
                BuildErrorCode.BindTrackNotFound,
                BuildErrorCode.BindTargetMissingAnimator,
                BuildErrorCode.BindTargetDuplicated,
                BuildErrorCode.BindTargetNotFound
            }));
            Assert.That(errors.Select(error => error.LineNumber), Is.EquivalentTo(new int?[] { 10, 11, 12, 13 }));
        }

        [Test]
        public void ReportsDuplicatedAnimationTracks()
        {
            var target = new GameObject("Character");
            target.AddComponent<Animator>();
            timeline.CreateTrack<AnimationTrack>(null, "Move");
            var duplicate = timeline.CreateTrack<AnimationTrack>(null, "Other");
            duplicate.name = "Move";

            var errors = new TrackBindingApplier().Apply(director, timeline, scene, directorObject,
                new[] { new SceneBindRow(9, "Move", "Character") });

            Assert.That(errors.Single().Code, Is.EqualTo(BuildErrorCode.BindTrackDuplicated));
        }
    }
}
