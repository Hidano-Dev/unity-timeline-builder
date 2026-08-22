using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class MultiTimelineBuildIntegrationTests
    {
        private const string FixtureDirectory = "Assets/UnityTimelineBuilder/Tests/MultiTimelineBuildFixtures";
        private const string SheetPath = FixtureDirectory + "/multi-timeline.csv";
        private const string AnimationPath = FixtureDirectory + "/character.anim";
        private const string ScenePrefabPath = FixtureDirectory + "/Character.prefab";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/MultiTimelineBuildOutput";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(FixtureDirectory);
            EnsureFolder(OutputDirectory);

            var animation = new AnimationClip { name = "MultiTimelineCharacter" };
            animation.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0, 0, 1, 1));
            AssetDatabase.CreateAsset(animation, AnimationPath);

            var character = new GameObject("Character");
            var characterRoot = new GameObject("CharacterRoot");
            characterRoot.transform.SetParent(character.transform);
            characterRoot.AddComponent<Animator>();
            PrefabUtility.SaveAsPrefabAsset(character, ScenePrefabPath);
            UnityEngine.Object.DestroyImmediate(character);

            File.WriteAllText(ProjectPath(SheetPath),
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath,timeline\n" +
                "Animation,Character,OpeningClip,0,0,1," + AnimationPath + ",Opening\n" +
                "Scene,OpeningScene,,,,,,Opening\n" +
                "ScenePrefab,,,,,," + ScenePrefabPath + ",Opening\n" +
                "SceneBind,Character,,,,,CharacterRoot,Opening\n" +
                "Animation,Character,BattleClip,2,0,1," + AnimationPath + ",Battle\n" +
                "Scene,BattleScene,,,,,,Battle\n" +
                "ScenePrefab,,,,,," + ScenePrefabPath + ",Battle\n" +
                "SceneBind,Character,,,,,CharacterRoot,Battle\n");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(OutputDirectory);
            AssetDatabase.DeleteAsset(ScenePrefabPath);
            AssetDatabase.DeleteAsset(AnimationPath);
            if (File.Exists(ProjectPath(SheetPath)))
                File.Delete(ProjectPath(SheetPath));
            AssetDatabase.DeleteAsset(FixtureDirectory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void BuildsAllGroupsInFirstSeenOrderWithScopedTracksAndSceneReferences()
        {
            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = ProjectPath(SheetPath),
                OutputDirectory = OutputDirectory,
                ImportDirectory = FixtureDirectory + "/Imported"
            });

            Assert.That(result.Success, Is.True, FormatErrors(result));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Outputs, Has.Count.EqualTo(2));
            Assert.That(result.Outputs.Select(output => output.TimelineName),
                Is.EqualTo(new[] { "Opening", "Battle" }));
            Assert.That(result.Outputs.SelectMany(output => new[]
                { output.TimelineAssetPath, output.PrefabPath, output.ScenePath }),
                Is.EqualTo(new[]
                {
                    OutputDirectory + "/Opening.playable",
                    OutputDirectory + "/Opening.prefab",
                    OutputDirectory + "/OpeningScene.unity",
                    OutputDirectory + "/Battle.playable",
                    OutputDirectory + "/Battle.prefab",
                    OutputDirectory + "/BattleScene.unity"
                }));

            var openingTimeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(
                OutputDirectory + "/Opening.playable");
            var battleTimeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(
                OutputDirectory + "/Battle.playable");
            Assert.That(openingTimeline.GetOutputTracks(), Has.Length.EqualTo(1));
            Assert.That(battleTimeline.GetOutputTracks(), Has.Length.EqualTo(1));
            Assert.That(openingTimeline.GetOutputTracks().Single().name, Is.EqualTo("Character"));
            Assert.That(battleTimeline.GetOutputTracks().Single().name, Is.EqualTo("Character"));
            Assert.That(openingTimeline.GetOutputTracks().Single().GetClips().Single().displayName,
                Is.EqualTo("OpeningClip"));
            Assert.That(battleTimeline.GetOutputTracks().Single().GetClips().Single().displayName,
                Is.EqualTo("BattleClip"));

            AssertSceneReferencesTimeline(OutputDirectory + "/OpeningScene.unity",
                OutputDirectory + "/Opening.playable");
            AssertSceneReferencesTimeline(OutputDirectory + "/BattleScene.unity",
                OutputDirectory + "/Battle.playable");
        }

        private static void AssertSceneReferencesTimeline(string scenePath, string expectedTimelinePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var expectedTimeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(expectedTimelinePath);
            var director = scene.GetRootGameObjects()
                .Select(root => root.GetComponent<PlayableDirector>())
                .Single(component => component != null);
            Assert.That(director.playableAsset, Is.SameAs(expectedTimeline));

            var track = expectedTimeline.GetOutputTracks().Single();
            Assert.That(director.GetGenericBinding(track), Is.Not.Null);
            Assert.That(director.GetGenericBinding(track).name, Is.EqualTo("CharacterRoot"));
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
