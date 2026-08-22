using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class RenameAndFailFastIntegrationTests
    {
        private const string FixtureDirectory = "Assets/UnityTimelineBuilder/Tests/RenameAndFailFastFixtures";
        private const string SheetPath = FixtureDirectory + "/integration.csv";
        private const string AnimationPath = FixtureDirectory + "/character.anim";
        private const string PrefabPath = FixtureDirectory + "/Character.prefab";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/RenameAndFailFastOutput";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(FixtureDirectory);
            EnsureFolder(OutputDirectory);

            var animation = new AnimationClip { name = "RenameAndFailFastCharacter" };
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
        public void PropagatesCaseInsensitiveRenameToSceneReferenceAndLogsWarning()
        {
            WriteSheet(
                "Animation,MainTrack,MainClip,0,0,1," + AnimationPath + ",Main\n" +
                "Scene,Shot,,,,,,Main\n" +
                "ScenePrefab,,,,,," + PrefabPath + ",Main\n" +
                "SceneBind,MainTrack,,,,,CharacterRoot,Main\n" +
                "Animation,mainTrack,mainClip,0,0,1," + AnimationPath + ",main\n" +
                "Scene,shot,,,,,,main\n" +
                "ScenePrefab,,,,,," + PrefabPath + ",main\n" +
                "SceneBind,mainTrack,,,,,CharacterRoot,main\n");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                ".*output renamed.*main \\(1\\).*"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                ".*output renamed.*shot \\(1\\).*"));

            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = ProjectPath(SheetPath),
                OutputDirectory = OutputDirectory,
                ImportDirectory = FixtureDirectory + "/Imported"
            });

            Assert.That(result.Success, Is.True, FormatErrors(result));
            Assert.That(result.Outputs.Select(output => output.TimelineAssetPath), Is.EqualTo(new[]
            {
                OutputDirectory + "/Main.playable",
                OutputDirectory + "/main (1).playable"
            }));
            Assert.That(result.Outputs[1].PrefabPath, Is.EqualTo(OutputDirectory + "/main (1).prefab"));
            Assert.That(result.Outputs[1].ScenePath, Is.EqualTo(OutputDirectory + "/shot (1).unity"));

            AssertSceneReferencesTimeline(OutputDirectory + "/shot (1).unity",
                OutputDirectory + "/main (1).playable");
        }

        [Test]
        public void GenerationFailureLeavesEarlierOutputsAndIdentifiesFailingTimeline()
        {
            WriteSheet(
                "Animation,Track,FirstClip,0,0,1," + AnimationPath + ",Main\n" +
                "Animation,Track,SecondClip,0,0,1," + AnimationPath + ",main\n");
            EnsureFolder(OutputDirectory + "/main (1).playable");

            LogAssert.ignoreFailingMessages = true;
            BuildResult result;
            try
            {
                result = TimelineBuilder.Build(new BuildRequest
                {
                    SheetPath = ProjectPath(SheetPath),
                    OutputDirectory = OutputDirectory,
                    ImportDirectory = FixtureDirectory + "/Imported"
                });
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }

            Assert.That(result.Success, Is.False);
            Assert.That(result.Outputs, Has.Count.EqualTo(2));
            Assert.That(result.Outputs[0].TimelineAssetPath, Is.EqualTo(OutputDirectory + "/Main.playable"));
            Assert.That(result.Outputs[1].TimelineAssetPath, Is.Null);
            Assert.That(result.Outputs[1].PrefabPath, Is.Null);
            Assert.That(result.Errors.Single().TimelineName, Is.EqualTo("main"));
            Assert.That(AssetDatabase.LoadAssetAtPath<TimelineAsset>(OutputDirectory + "/Main.playable"), Is.Not.Null);
        }

        [Test]
        public void ValidationAggregatesErrorsAcrossGroupsBeforeCreatingAnyAsset()
        {
            WriteSheet(
                "Animation,Track,FirstClip,0,0,1," + AnimationPath + ",Main\n" +
                "Scene,FirstScene,,,,,,Main\n" +
                "ScenePrefab,,,,,," + FixtureDirectory + "/MissingA.prefab,Main\n" +
                "Animation,Track,SecondClip,0,0,1," + AnimationPath + ",Battle\n" +
                "Scene,SecondScene,,,,,,Battle\n" +
                "ScenePrefab,,,,,," + FixtureDirectory + "/MissingB.prefab,Battle\n");

            LogAssert.ignoreFailingMessages = true;
            BuildResult result;
            try
            {
                result = TimelineBuilder.Build(new BuildRequest
                {
                    SheetPath = ProjectPath(SheetPath),
                    OutputDirectory = OutputDirectory,
                    ImportDirectory = FixtureDirectory + "/Imported"
                });
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }

            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(2));
            Assert.That(result.Errors.Select(error => error.TimelineName), Is.EquivalentTo(new[] { "Main", "Battle" }));
            Assert.That(result.Outputs, Is.Empty);
            Assert.That(AssetDatabase.LoadAssetAtPath<TimelineAsset>(OutputDirectory + "/Main.playable"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<TimelineAsset>(OutputDirectory + "/Battle.playable"), Is.Null);
        }

        private void WriteSheet(string rows)
        {
            File.WriteAllText(ProjectPath(SheetPath),
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath,timeline\n" + rows);
            AssetDatabase.Refresh();
        }

        private static void AssertSceneReferencesTimeline(string scenePath, string timelinePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var director = scene.GetRootGameObjects()
                .Select(root => root.GetComponent<PlayableDirector>())
                .Single(component => component != null);
            Assert.That(director.playableAsset,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath)));
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
