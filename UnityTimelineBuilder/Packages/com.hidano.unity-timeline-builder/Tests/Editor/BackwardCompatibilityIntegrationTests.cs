using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.TestTools;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class BackwardCompatibilityIntegrationTests
    {
        private const string FixtureDirectory = "Assets/UnityTimelineBuilder/Tests/BackwardCompatibilityFixtures";
        private const string SheetPath = FixtureDirectory + "/legacy.csv";
        private const string AnimationPath = FixtureDirectory + "/character.anim";
        private const string PrefabPath = FixtureDirectory + "/Character.prefab";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/BackwardCompatibilityOutput";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(FixtureDirectory);
            EnsureFolder(OutputDirectory);

            var animation = new AnimationClip { name = "BackwardCompatibilityCharacter" };
            animation.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0, 0, 1, 1));
            AssetDatabase.CreateAsset(animation, AnimationPath);

            var character = new GameObject("Character");
            var characterRoot = new GameObject("CharacterRoot");
            characterRoot.transform.SetParent(character.transform);
            characterRoot.AddComponent<Animator>();
            PrefabUtility.SaveAsPrefabAsset(character, PrefabPath);
            UnityEngine.Object.DestroyImmediate(character);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(OutputDirectory);
            AssetDatabase.DeleteAsset(PrefabPath);
            AssetDatabase.DeleteAsset(AnimationPath);
            if (File.Exists(ProjectPath(SheetPath)))
                File.Delete(ProjectPath(SheetPath));
            AssetDatabase.DeleteAsset(FixtureDirectory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void HeaderlessSheetPreservesLegacyNamesOutputsAndResultProperties()
        {
            WriteSheet(
                "Animation,Character,Walk,0,0,1," + AnimationPath + "\n");

            var result = Build("LegacyAsset");

            Assert.That(result.Success, Is.True, FormatErrors(result));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.TimelineAssetPath, Is.EqualTo(OutputDirectory + "/LegacyAsset/Timelines/LegacyAsset.playable"));
            Assert.That(result.PrefabPath, Is.EqualTo(OutputDirectory + "/LegacyAsset/Prefabs/LegacyAsset.prefab"));
            Assert.That(result.ScenePath, Is.Null);
            Assert.That(result.Outputs, Has.Count.EqualTo(1));
            Assert.That(result.Outputs[0].TimelineName, Is.Null);
            Assert.That(result.Outputs[0].ResolvedAssetName, Is.EqualTo("LegacyAsset"));
            Assert.That(AssetDatabase.LoadAssetAtPath<TimelineAsset>(result.TimelineAssetPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath), Is.Not.Null);
        }

        [Test]
        public void HeaderWithNoTimelineColumnPreservesLegacySceneOutputAndBinding()
        {
            WriteSheet(
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath\n" +
                "Animation,Character,Walk,0,0,1," + AnimationPath + "\n" +
                "Scene,LegacyScene,,,,,\n" +
                "ScenePrefab,,,,,," + PrefabPath + "\n" +
                "SceneBind,Character,,,,,CharacterRoot\n");

            var result = Build("LegacySceneAsset");

            Assert.That(result.Success, Is.True, FormatErrors(result));
            Assert.That(result.TimelineAssetPath, Is.EqualTo(OutputDirectory + "/LegacyScene/Timelines/LegacySceneAsset.playable"));
            Assert.That(result.PrefabPath, Is.EqualTo(OutputDirectory + "/LegacyScene/Prefabs/LegacySceneAsset.prefab"));
            Assert.That(result.ScenePath, Is.EqualTo(OutputDirectory + "/LegacyScene/Scenes/LegacyScene.unity"));
            Assert.That(result.Outputs.Single().HasScenePlan, Is.True);

            var scene = EditorSceneManager.OpenScene(result.ScenePath, OpenSceneMode.Single);
            var director = scene.GetRootGameObjects()
                .Select(root => root.GetComponent<PlayableDirector>())
                .Single(component => component != null);
            Assert.That(director.playableAsset,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TimelineAsset>(result.TimelineAssetPath)));
        }

        [Test]
        public void TimelineColumnAndAssetNameStopsBeforeCreatingLegacyOutputs()
        {
            WriteSheet(
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath,timeline\n" +
                "Animation,Character,Walk,0,0,1," + AnimationPath + ",NewTimeline\n");

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                ".*AssetNameConflict.*ExplicitAssetName.*"));
            var result = Build("ExplicitAssetName");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Code, Is.EqualTo(BuildErrorCode.AssetNameConflict));
            Assert.That(result.Errors[0].TimelineName, Is.Null);
            Assert.That(result.Errors[0].Message, Does.Contain("ExplicitAssetName"));
            Assert.That(result.Outputs, Is.Empty);
            Assert.That(AssetDatabase.LoadAssetAtPath<TimelineAsset>(
                OutputDirectory + "/ExplicitAssetName/Timelines/ExplicitAssetName.playable"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                OutputDirectory + "/ExplicitAssetName/Prefabs/ExplicitAssetName.prefab"), Is.Null);
        }

        [Test]
        public void LegacySceneFailureRetainsOnlyTimelineAndPrefabAsBefore()
        {
            WriteSheet(
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath\n" +
                "Animation,Character,Walk,0,0,1," + AnimationPath + "\n" +
                "Scene,LegacyScene,,,,,\n" +
                "SceneBind,Character,,,,,MissingCharacter\n");

            var result = Build("LegacyFailure");

            Assert.That(result.Success, Is.False);
            Assert.That(result.TimelineAssetPath, Is.EqualTo(OutputDirectory + "/LegacyScene/Timelines/LegacyFailure.playable"));
            Assert.That(result.PrefabPath, Is.EqualTo(OutputDirectory + "/LegacyScene/Prefabs/LegacyFailure.prefab"));
            Assert.That(result.ScenePath, Is.Null);
            Assert.That(result.Errors.Any(error => error.Code == BuildErrorCode.BindTargetNotFound), Is.True);
            Assert.That(File.Exists(ProjectPath(OutputDirectory + "/LegacyScene/Scenes/LegacyScene.unity")), Is.False);
        }

        private BuildResult Build(string assetName)
        {
            return TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = ProjectPath(SheetPath),
                OutputDirectory = OutputDirectory,
                AssetName = assetName,
                ImportDirectory = FixtureDirectory + "/Imported"
            });
        }

        private void WriteSheet(string contents)
        {
            File.WriteAllText(ProjectPath(SheetPath), contents);
            AssetDatabase.Refresh();
        }

        private static string FormatErrors(BuildResult result)
        {
            return string.Join("\n", result.Errors.Select(error => error.Code + ": " + error.Message));
        }

        private static string ProjectPath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
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
