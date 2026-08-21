using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.TestTools;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class SceneFactoryTests
    {
        private const string FixtureDirectory = "Assets/UnityTimelineBuilder/Tests/SceneFactoryFixtures";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/SceneFactoryOutput";
        private const string SheetPath = FixtureDirectory + "/scene-factory.csv";
        private const string AnimationPath = FixtureDirectory + "/SceneFactoryAnimation.anim";
        private const string CharacterPrefabPath = FixtureDirectory + "/Character.prefab";
        private const string PropPrefabPath = FixtureDirectory + "/Prop.prefab";
        private const string TimelinePath = OutputDirectory + "/SceneFactory.playable";
        private const string PrefabPath = OutputDirectory + "/SceneFactory.prefab";
        private const string ScenePath = OutputDirectory + "/SceneFactory.unity";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(FixtureDirectory);
            EnsureFolder(OutputDirectory);

            var animation = new AnimationClip { name = "SceneFactoryAnimation" };
            animation.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0, 0, 1, 1));
            AssetDatabase.CreateAsset(animation, AnimationPath);
            CreateAnimatorPrefab(CharacterPrefabPath, "Character");
            CreateAnimatorPrefab(PropPrefabPath, "Prop");
            WriteSheet();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.DeleteAsset(PrefabPath);
            AssetDatabase.DeleteAsset(TimelinePath);
            AssetDatabase.DeleteAsset(CharacterPrefabPath);
            AssetDatabase.DeleteAsset(PropPrefabPath);
            AssetDatabase.DeleteAsset(AnimationPath);
            AssetDatabase.DeleteAsset(OutputDirectory);
            AssetDatabase.DeleteAsset(FixtureDirectory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void CreatesSceneWithDirectorTimelineAndAllPrefabInstances()
        {
            var result = Build();

            Assert.That(result.Success, Is.True, FormatErrors(result));
            Assert.That(result.ScenePath, Is.EqualTo(ScenePath));
            Assert.That(File.Exists(ProjectPath(ScenePath)), Is.True);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var directorObject = roots.Single(root => root.name == "SceneFactory");
            var director = directorObject.GetComponent<PlayableDirector>();
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);

            Assert.That(director, Is.Not.Null);
            Assert.That(director.playableAsset, Is.SameAs(timeline));
            Assert.That(roots.Count(root => PrefabUtility.IsAnyPrefabInstanceRoot(root)), Is.EqualTo(2));
            Assert.That(roots.Single(root => root.name == "Character").GetComponent<Animator>(), Is.Not.Null);
            Assert.That(roots.Single(root => root.name == "Prop").GetComponent<Animator>(), Is.Not.Null);
        }

        [Test]
        public void ReopeningSavedScenePreservesBindingAndLeavesUnboundTrackUnset()
        {
            var result = Build();
            Assert.That(result.Success, Is.True, FormatErrors(result));

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var director = scene.GetRootGameObjects().Single(root => root.name == "SceneFactory")
                .GetComponent<PlayableDirector>();
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            var characterTrack = timeline.GetOutputTracks().OfType<AnimationTrack>()
                .Single(track => track.name == "Character");
            var propTrack = timeline.GetOutputTracks().OfType<AnimationTrack>()
                .Single(track => track.name == "Prop");

            Assert.That(director.GetGenericBinding(characterTrack), Is.TypeOf<Animator>());
            Assert.That(director.GetGenericBinding(characterTrack), Is.SameAs(
                scene.GetRootGameObjects().Single(root => root.name == "Character").GetComponent<Animator>()));
            Assert.That(director.GetGenericBinding(propTrack), Is.Null);
        }

        [Test]
        public void OverwritingScenePreservesGuidAndLogsOverwrite()
        {
            var first = Build();
            Assert.That(first.Success, Is.True, FormatErrors(first));
            var guid = AssetDatabase.AssetPathToGUID(ScenePath);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(".*Overwriting TimelineAsset.*"));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(".*Overwriting Prefab.*"));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(".*Overwriting Scene.*"));
            var second = Build();

            Assert.That(second.Success, Is.True, FormatErrors(second));
            Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.EqualTo(guid));
        }

        [Test]
        public void BindingFailureDoesNotSaveScene()
        {
            WriteSheet(includeMissingBinding: true);
            AssetDatabase.Refresh();
            LogAssert.ignoreFailingMessages = true;
            BuildResult result;
            try
            {
                result = Build();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }

            Assert.That(result.Success, Is.False);
            Assert.That(result.ScenePath, Is.Null);
            Assert.That(result.Errors.Any(error => error.Code == BuildErrorCode.BindTargetNotFound), Is.True);
            Assert.That(File.Exists(ProjectPath(ScenePath)), Is.False);
        }

        private static BuildResult Build()
        {
            return TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = ProjectPath(SheetPath),
                OutputDirectory = OutputDirectory,
                AssetName = "SceneFactory",
                ImportDirectory = FixtureDirectory + "/Imported"
            });
        }

        private static void WriteSheet(bool includeMissingBinding = false)
        {
            var binding = includeMissingBinding ? "SceneBind,Character,,,,,MissingCharacter\n" :
                "SceneBind,Character,,,,,Character\n";
            File.WriteAllText(ProjectPath(SheetPath),
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath\n" +
                "Animation,Character,Walk,0,0,1," + AnimationPath + "\n" +
                "Animation,Prop,Idle,1,0,1," + AnimationPath + "\n" +
                "Scene,SceneFactory,\n" +
                "ScenePrefab,,,,,," + CharacterPrefabPath + "\n" +
                "ScenePrefab,,,,,," + PropPrefabPath + "\n" + binding);
        }

        private static void CreateAnimatorPrefab(string path, string name)
        {
            var gameObject = new GameObject(name);
            gameObject.AddComponent<Animator>();
            PrefabUtility.SaveAsPrefabAsset(gameObject, path);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private static string ProjectPath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string FormatErrors(BuildResult result)
        {
            return string.Join("\n", result.Errors.Select(error => error.Code + ": " + error.Message));
        }

        private static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
