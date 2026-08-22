using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class TimelineBuilderCliTests
    {
        private const string FixtureDirectory = "Assets/UnityTimelineBuilder/Tests/CliFixtures";
        private const string SheetPath = FixtureDirectory + "/cli.csv";
        private const string AnimationPath = FixtureDirectory + "/character.anim";
        private const string PrefabPath = FixtureDirectory + "/Character.prefab";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/CliOutput";

        [SetUp]
        public void SetUp()
        {
            EnsureFolder(FixtureDirectory);
            EnsureFolder(OutputDirectory);

            var animation = new AnimationClip { name = "CliCharacter" };
            animation.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0, 0, 1, 1));
            AssetDatabase.CreateAsset(animation, AnimationPath);

            var character = new GameObject("Character");
            var characterRoot = new GameObject("CharacterRoot");
            characterRoot.transform.SetParent(character.transform);
            characterRoot.AddComponent<Animator>();
            PrefabUtility.SaveAsPrefabAsset(character, PrefabPath);
            Object.DestroyImmediate(character);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
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
        public void RunLogsAllOutputsForMultipleTimelinesAndReturnsSuccess()
        {
            WriteSheet(
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath,timeline\n" +
                "Animation,Character,OpeningClip,0,0,1," + AnimationPath + ",Opening\n" +
                "Scene,OpeningScene,,,,,,Opening\n" +
                "ScenePrefab,,,,,," + PrefabPath + ",Opening\n" +
                "SceneBind,Character,,,,,CharacterRoot,Opening\n" +
                "Animation,Character,BattleClip,0,0,1," + AnimationPath + ",Battle\n" +
                "Scene,BattleScene,,,,,,Battle\n" +
                "ScenePrefab,,,,,," + PrefabPath + ",Battle\n" +
                "SceneBind,Character,,,,,CharacterRoot,Battle\n");

            ExpectLog(".*TimelineAsset: " + OutputDirectory + "/OpeningScene/Timelines/Opening\\.playable");
            ExpectLog(".*Prefab: " + OutputDirectory + "/OpeningScene/Prefabs/Opening\\.prefab");
            ExpectLog(".*Scene: " + OutputDirectory + "/OpeningScene/Scenes/OpeningScene\\.unity");
            ExpectLog(".*TimelineAsset: " + OutputDirectory + "/BattleScene/Timelines/Battle\\.playable");
            ExpectLog(".*Prefab: " + OutputDirectory + "/BattleScene/Prefabs/Battle\\.prefab");
            ExpectLog(".*Scene: " + OutputDirectory + "/BattleScene/Scenes/BattleScene\\.unity");

            var exitCode = RunCli(SheetPath);

            Assert.That(exitCode, Is.EqualTo(0));
        }

        [Test]
        public void RunReportsTimelineErrorAndReturnsBuildFailure()
        {
            WriteSheet(
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath,timeline\n" +
                "Animation,Character,MissingClip,0,0,1,Assets/missing-cli-resource.anim,Broken\n");

            var errors = new System.Collections.Generic.List<string>();
            Application.LogCallback handler = (condition, stackTrace, type) =>
            {
                if (type == LogType.Error)
                    errors.Add(condition);
            };
            Application.logMessageReceived += handler;
            LogAssert.ignoreFailingMessages = true;
            int exitCode;
            try
            {
                exitCode = RunCli(SheetPath);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                Application.logMessageReceived -= handler;
            }

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(errors.Any(error => error.Contains("ResourceNotFound")), Is.True);
            Assert.That(errors.Any(error => error.Contains("[timeline: Broken]")), Is.True);
        }

        [Test]
        public void RunPreservesLegacyOutputLogFormat()
        {
            WriteSheet(
                "Animation,Character,Walk,0,0,1," + AnimationPath + "\n");

            ExpectLog(".*TimelineAsset: " + OutputDirectory + "/LegacyAsset/Timelines/LegacyAsset\\.playable");
            ExpectLog(".*Prefab: " + OutputDirectory + "/LegacyAsset/Prefabs/LegacyAsset\\.prefab");

            var exitCode = RunCli(SheetPath, "LegacyAsset");

            Assert.That(exitCode, Is.EqualTo(0));
        }

        [Test]
        public void RunReturnsArgumentFailureWhenRequiredOptionsAreMissing()
        {
            ExpectError(".*引数.*");
            var exitCode = Hidano.UnityTimelineBuilder.Editor.TimelineBuilderCli.Run(
                new[] { "Unity", "-batchmode" });

            Assert.That(exitCode, Is.EqualTo(2));
        }

        [Test]
        public void RunReturnsArgumentFailureWhenOutputDirectoryIsOutsideAssets()
        {
            ExpectError(".*引数.*");
            var exitCode = Hidano.UnityTimelineBuilder.Editor.TimelineBuilderCli.Run(
                new[]
                {
                    "Unity",
                    "-sheetPath", "Assets/build.csv",
                    "-outputDir", "ProjectSettings"
                });

            Assert.That(exitCode, Is.EqualTo(2));
        }

        [Test]
        public void RunReturnsBuildFailureWhenSheetDoesNotExist()
        {
            ExpectError(".*SheetNotFound.*");
            ExpectError(".*構築に失敗しました.*");
            ExpectError(".*SheetNotFound.*");
            var exitCode = Hidano.UnityTimelineBuilder.Editor.TimelineBuilderCli.Run(
                new[]
                {
                    "Unity",
                    "-sheetPath", "Assets/missing-build-sheet.csv",
                    "-outputDir", "Assets/Generated"
                });

            Assert.That(exitCode, Is.EqualTo(1));
        }

        [Test]
        public void FormatErrorReportsTimelineNameWhenPresent()
        {
            var error = new BuildError(BuildErrorCode.RowValidationError, 5, "sheet.csv",
                "Invalid row", "Title");
            var formatError = typeof(Hidano.UnityTimelineBuilder.Editor.TimelineBuilderCli)
                .GetMethod("FormatError", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { error });

            Assert.That(formatError, Is.EqualTo(
                "RowValidationError (行 5) [sheet.csv] [timeline: Title]: Invalid row"));
        }

        private static void ExpectError(string pattern)
        {
            LogAssert.Expect(LogType.Error, new Regex(pattern));
        }

        private static void ExpectLog(string pattern)
        {
            LogAssert.Expect(LogType.Log, new Regex(pattern));
        }

        private static int RunCli(string sheetPath, string assetName = null)
        {
            var args = new System.Collections.Generic.List<string>
            {
                "Unity", "-sheetPath", sheetPath, "-outputDir", OutputDirectory
            };
            if (assetName != null)
            {
                args.Add("-assetName");
                args.Add(assetName);
            }
            return Hidano.UnityTimelineBuilder.Editor.TimelineBuilderCli.Run(args.ToArray());
        }

        private static void WriteSheet(string content)
        {
            File.WriteAllText(ProjectPath(SheetPath), content);
            AssetDatabase.Refresh();
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
