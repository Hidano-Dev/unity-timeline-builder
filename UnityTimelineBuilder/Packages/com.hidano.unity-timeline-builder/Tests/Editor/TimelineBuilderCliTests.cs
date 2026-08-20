using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class TimelineBuilderCliTests
    {
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

        private static void ExpectError(string pattern)
        {
            LogAssert.Expect(LogType.Error, new Regex(pattern));
        }
    }
}
