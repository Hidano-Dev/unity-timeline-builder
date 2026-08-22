using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class BuildSheetParserTests
    {
        private static BuildSheetParser CreateParser()
        {
            return new BuildSheetParser(type =>
                string.Equals(type, "Audio", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Animation", System.StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void ParsesHeaderWithShuffledColumnsCaseInsensitively()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "DURATION", "TrackName", "resourcePath", "TRACKTYPE", "clipName", "clipIn", "startTime" },
                new[] { "3.5", "BGM", "Assets/intro.wav", "audio", "Intro", "0.25", "1.5" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.Rows, Has.Count.EqualTo(1));
            Assert.That(outcome.Rows[0].LineNumber, Is.EqualTo(2));
            Assert.That(outcome.Rows[0].TrackType, Is.EqualTo("audio"));
            Assert.That(outcome.Rows[0].StartTime, Is.EqualTo(1.5));
            Assert.That(outcome.Rows[0].ClipIn, Is.EqualTo(0.25));
            Assert.That(outcome.Rows[0].Duration, Is.EqualTo(3.5));
        }

        [Test]
        public void RecognizesTimelineColumnWhenShuffledAndCaseDiffers()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "TIMELINE", "resourcePath", "TRACKTYPE", "startTime", "trackName", "clipIn" },
                new[] { "Main", "Assets/intro.wav", "Audio", "0", "Music", "0" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.HasTimelineColumn, Is.True);
            Assert.That(outcome.Groups, Has.Count.EqualTo(1));
            Assert.That(outcome.Groups[0].TimelineName, Is.EqualTo("Main"));
            Assert.That(outcome.Groups[0].Rows[0].TrackName, Is.EqualTo("Music"));
        }

        [Test]
        public void UsesDefaultColumnOrderAndLogsWarningWhenHeaderIsAbsent()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "Audio", "BGM", "Intro", "0", "0", "2", "Assets/intro.wav" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.Rows, Has.Count.EqualTo(1));
            Assert.That(outcome.Rows[0].ResourcePath, Is.EqualTo("Assets/intro.wav"));
            Assert.That(outcome.WarningMessage, Does.Contain("ヘッダー未検出"));
        }

        [Test]
        public void IgnoresTimelineLikeValuesWhenHeaderIsAbsent()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "Audio", "BGM", "Intro", "0", "0", "2", "Assets/intro.wav", "IgnoredTimeline" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.HasTimelineColumn, Is.False);
            Assert.That(outcome.Groups, Has.Count.EqualTo(1));
            Assert.That(outcome.Groups[0].TimelineName, Is.Null);
            Assert.That(outcome.Groups[0].Rows, Has.Count.EqualTo(1));
        }

        [Test]
        public void GroupsInterleavedRowsInFirstAppearanceOrder()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath", "timeline" },
                new[] { "Audio", "A1", "Clip", "0", "0", "1", "Assets/a1.wav", "First" },
                new[] { "Audio", "B1", "Clip", "0", "0", "1", "Assets/b1.wav", "Second" },
                new[] { "Audio", "A2", "Clip", "0", "0", "1", "Assets/a2.wav", "First" },
                new[] { "Audio", "B2", "Clip", "0", "0", "1", "Assets/b2.wav", "Second" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.Groups.Select(group => group.TimelineName), Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(outcome.Groups[0].FirstLineNumber, Is.EqualTo(2));
            Assert.That(outcome.Groups[1].FirstLineNumber, Is.EqualTo(3));
            Assert.That(outcome.Groups[0].Rows.Select(row => row.TrackName), Is.EqualTo(new[] { "A1", "A2" }));
            Assert.That(outcome.Groups[1].Rows.Select(row => row.TrackName), Is.EqualTo(new[] { "B1", "B2" }));
        }

        [Test]
        public void CollectsAllRowValidationErrorsWithOriginalLineNumbers()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath" },
                new[] { "Unknown", "Track", "Clip", "bad", "0", "1", "path" },
                new[] { "Audio", "Track", "Clip", "-1", "-0.1", "0", "path" },
                new[] { "Audio", "Track" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Rows, Is.Empty);
            Assert.That(outcome.Errors, Has.Count.EqualTo(6));
            Assert.That(outcome.Errors.Select(error => error.LineNumber), Is.EquivalentTo(new int?[] { 2, 2, 3, 3, 3, 4 }));
            Assert.That(outcome.Errors[0].Code, Is.EqualTo(BuildErrorCode.UnknownTrackType));
            Assert.That(outcome.Errors[1].Code, Is.EqualTo(BuildErrorCode.RowValidationError));
        }

        [Test]
        public void ReportsMissingHeaderColumns()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName" },
                new[] { "Audio", "BGM" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Has.Count.EqualTo(1));
            Assert.That(outcome.Errors[0].LineNumber, Is.EqualTo(1));
            Assert.That(outcome.Errors[0].Message, Does.Contain("resourcePath"));
            Assert.That(outcome.Errors[0].Message, Does.Not.Contain("clipName"));
            Assert.That(outcome.Errors[0].Message, Does.Not.Contain("duration"));
        }

        [Test]
        public void AllowsEmptyOptionalClipNameAndDuration()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath" },
                new[] { "Audio", "BGM", "", "0.5", "0", "", "Assets/intro.wav" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.Rows, Has.Count.EqualTo(1));
            Assert.That(outcome.Rows[0].ClipName, Is.Empty);
            Assert.That(outcome.Rows[0].Duration, Is.Null);
        }

        [Test]
        public void AllowsHeaderWithoutOptionalColumns()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "startTime", "clipIn", "resourcePath" },
                new[] { "Audio", "BGM", "0", "0", "Assets/intro.wav" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.Rows, Has.Count.EqualTo(1));
            Assert.That(outcome.Rows[0].ClipName, Is.Empty);
            Assert.That(outcome.Rows[0].Duration, Is.Null);
        }

        [Test]
        public void RejectsExplicitNonPositiveDurationEvenThoughEmptyIsAllowed()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath" },
                new[] { "Audio", "BGM", "Intro", "0", "0", "0", "Assets/intro.wav" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Rows, Is.Empty);
            Assert.That(outcome.Errors, Has.Count.EqualTo(1));
            Assert.That(outcome.Errors[0].Message, Does.Contain("duration"));
        }

        [Test]
        public void StripsSurroundingQuotesFromResourcePath()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath" },
                new[] { "Audio", "BGM", "Intro", "0", "0", "1", "\"C:\\Media\\my intro.wav\"" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.Rows, Has.Count.EqualTo(1));
            Assert.That(outcome.Rows[0].ResourcePath, Is.EqualTo("C:\\Media\\my intro.wav"));
        }

        [Test]
        public void CollectsSceneValidationErrorsWithLineNumbers()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath" },
                new[] { "ScenePrefab", "", "", "", "", "", "" },
                new[] { "SceneBind", "Walk", "", "", "", "", "Hero" },
                new[] { "Scene", "Bad|Scene", "", "", "", "", "" },
                new[] { "Scene", "Another", "", "", "", "", "" },
                new[] { "SceneBind", "Walk", "", "", "", "", "Hero2" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Has.Count.EqualTo(4));
            Assert.That(outcome.Errors.Select(error => error.LineNumber), Is.EquivalentTo(new int?[] { 2, 4, 5, 6 }));
            Assert.That(outcome.Errors.All(error => error.Code == BuildErrorCode.RowValidationError), Is.True);
        }

        [Test]
        public void ReportsMissingSceneBindValuesAndAllowsUnusedColumns()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath" },
                new[] { "Scene", "Sample", "ignored", "ignored", "ignored", "ignored", "" },
                new[] { "SceneBind", "", "ignored", "ignored", "ignored", "ignored", "" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Has.Count.EqualTo(2));
            Assert.That(outcome.Errors.Select(error => error.LineNumber), Is.EquivalentTo(new int?[] { 3, 3 }));
            Assert.That(outcome.ScenePlan.Definition.SceneName, Is.EqualTo("Sample"));
        }

        [Test]
        public void AggregatesSceneRowsWithoutMixingThemIntoClipRowsWithHeader()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath" },
                new[] { "Scene", "Gameplay", "", "", "", "", "Assets/Gameplay.timeline" },
                new[] { "ScenePrefab", "", "", "", "", "", "Assets/Hero.prefab" },
                new[] { "SceneBind", "Walk", "", "", "", "", "Hero" },
                new[] { "Audio", "Music", "Intro", "0", "0", "2", "Assets/intro.wav" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.Rows, Has.Count.EqualTo(1));
            Assert.That(outcome.Rows[0].TrackName, Is.EqualTo("Music"));
            Assert.That(outcome.ScenePlan, Is.Not.Null);
            Assert.That(outcome.ScenePlan.Definition.SceneName, Is.EqualTo("Gameplay"));
            Assert.That(outcome.ScenePlan.Definition.TimelineAssetPath, Is.EqualTo("Assets/Gameplay.timeline"));
            Assert.That(outcome.ScenePlan.Prefabs, Has.Count.EqualTo(1));
            Assert.That(outcome.ScenePlan.Prefabs[0].PrefabAssetPath, Is.EqualTo("Assets/Hero.prefab"));
            Assert.That(outcome.ScenePlan.Bindings, Has.Count.EqualTo(1));
            Assert.That(outcome.ScenePlan.Bindings[0].TrackName, Is.EqualTo("Walk"));
            Assert.That(outcome.ScenePlan.Bindings[0].GameObjectName, Is.EqualTo("Hero"));
        }

        [Test]
        public void AggregatesSceneRowsWithoutMixingThemIntoClipRowsWithHeaderlessSevenColumns()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "Scene", "Gameplay", "", "", "", "", "Assets/Gameplay.timeline" },
                new[] { "ScenePrefab", "", "", "", "", "", "Assets/Hero.prefab" },
                new[] { "SceneBind", "Walk", "", "", "", "", "Hero" },
                new[] { "Animation", "HeroTrack", "Run", "1", "0", "1.5", "Assets/run.anim" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.Rows, Has.Count.EqualTo(1));
            Assert.That(outcome.Rows[0].TrackType, Is.EqualTo("Animation"));
            Assert.That(outcome.ScenePlan.Definition.SceneName, Is.EqualTo("Gameplay"));
            Assert.That(outcome.ScenePlan.Prefabs[0].PrefabAssetPath, Is.EqualTo("Assets/Hero.prefab"));
            Assert.That(outcome.ScenePlan.Bindings[0].GameObjectName, Is.EqualTo("Hero"));
            Assert.That(outcome.WarningMessage, Is.Not.Null);
        }

        [Test]
        public void ReportsAllSceneValidationErrorsWithSourceLineNumbers()
        {
            var orphanRows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath" },
                new[] { "ScenePrefab", "", "", "", "", "", "Assets/Hero.prefab" },
                new[] { "SceneBind", "Walk", "", "", "", "", "Hero" },
            };

            var orphanOutcome = CreateParser().Parse(orphanRows);

            Assert.That(orphanOutcome.Rows, Is.Empty);
            Assert.That(orphanOutcome.Errors.Select(error => error.LineNumber),
                Is.EquivalentTo(new int?[] { 2, 3 }));
            Assert.That(orphanOutcome.Errors.All(error => error.Code == BuildErrorCode.RowValidationError), Is.True);

            var duplicateAndInvalidRows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath" },
                new[] { "Scene", "Bad|Scene", "", "", "", "", "" },
                new[] { "Scene", "Valid", "", "", "", "", "" },
                new[] { "SceneBind", "Walk", "", "", "", "", "Hero2" }
            };

            var duplicateAndInvalidOutcome = CreateParser().Parse(duplicateAndInvalidRows);

            Assert.That(duplicateAndInvalidOutcome.Rows, Is.Empty);
            Assert.That(duplicateAndInvalidOutcome.Errors.Select(error => error.LineNumber),
                Is.EquivalentTo(new int?[] { 2, 3 }));
            Assert.That(duplicateAndInvalidOutcome.Errors.All(error => error.Code == BuildErrorCode.RowValidationError), Is.True);
        }

        [Test]
        public void KeepsScenePlanNullAndClipParsingUnchangedForLegacyInput()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath" },
                new[] { "Audio", "BGM", "Intro", "1.25", "0.5", "3", "Assets/intro.wav" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.ScenePlan, Is.Null);
            Assert.That(outcome.Rows, Has.Count.EqualTo(1));
            Assert.That(outcome.Rows[0].LineNumber, Is.EqualTo(2));
            Assert.That(outcome.Rows[0].TrackType, Is.EqualTo("Audio"));
            Assert.That(outcome.Rows[0].TrackName, Is.EqualTo("BGM"));
            Assert.That(outcome.Rows[0].ClipName, Is.EqualTo("Intro"));
            Assert.That(outcome.Rows[0].StartTime, Is.EqualTo(1.25));
            Assert.That(outcome.Rows[0].ClipIn, Is.EqualTo(0.5));
            Assert.That(outcome.Rows[0].Duration, Is.EqualTo(3));
            Assert.That(outcome.Rows[0].ResourcePath, Is.EqualTo("Assets/intro.wav"));
            Assert.That(outcome.Groups, Has.Count.EqualTo(1));
            Assert.That(outcome.Groups[0].TimelineName, Is.Null);
            Assert.That(outcome.Groups[0].Rows, Is.EqualTo(outcome.Rows));
        }

        [Test]
        public void RequiresTimelineValueOnEveryDataRowWhenTimelineColumnExists()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath", "timeline" },
                new[] { "Audio", "A", "Clip", "0", "0", "1", "Assets/a.wav", "Main" },
                new[] { "Audio", "B", "Clip", "0", "0", "1", "Assets/b.wav", "" },
                new[] { "Audio", "C", "Clip", "0", "0", "1", "Assets/c.wav", "   " }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Has.Count.EqualTo(2));
            Assert.That(outcome.Errors.Select(error => error.LineNumber), Is.EquivalentTo(new int?[] { 3, 4 }));
            Assert.That(outcome.Errors.All(error => error.Message.Contains("timeline")), Is.True);
            Assert.That(outcome.Errors.All(error => error.Message.Contains("必須") || error.Message.Contains("required")), Is.True);
        }

        [Test]
        public void AppliesFileNameValidationToTimelineNamesAndSceneNames()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath", "timeline" },
                new[] { "Audio", "A", "Clip", "0", "0", "1", "Assets/a.wav", "Bad|Timeline" },
                new[] { "Audio", "B", "Clip", "0", "0", "1", "Assets/b.wav", "." },
                new[] { "Scene", "Valid", "", "", "", "", "", "Good" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Has.Count.EqualTo(2));
            Assert.That(outcome.Errors.Select(error => error.LineNumber), Is.EquivalentTo(new int?[] { 2, 3 }));
            Assert.That(outcome.Errors[0].Message, Does.Contain("Bad|Timeline"));
            Assert.That(outcome.Errors[1].Message, Does.Contain("."));
        }

        [Test]
        public void ReportsTimelineBlankAndInvalidNameErrorsWithSourceLineNumbers()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath", "timeline" },
                new[] { "Audio", "Blank", "Clip", "0", "0", "1", "Assets/blank.wav", "" },
                new[] { "Audio", "Invalid", "Clip", "0", "0", "1", "Assets/invalid.wav", "Bad|Timeline" },
                new[] { "Audio", "Trailing", "Clip", "0", "0", "1", "Assets/trailing.wav", "Trailing. " }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Has.Count.EqualTo(3));
            Assert.That(outcome.Errors.Select(error => error.LineNumber),
                Is.EqualTo(new int?[] { 2, 3, 4 }));
            Assert.That(outcome.Errors.All(error => error.Code == BuildErrorCode.RowValidationError), Is.True);
            Assert.That(outcome.Errors[0].Message, Does.Contain("timeline"));
            Assert.That(outcome.Errors[1].Message, Does.Contain("Bad|Timeline"));
            Assert.That(outcome.Errors[2].Message, Does.Contain("Trailing."));
        }

        [Test]
        public void AllowsOneScenePerTimelineGroupAndScopesBindingDuplicatesToEachGroup()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath", "timeline" },
                new[] { "Scene", "FirstScene", "", "", "", "", "", "First" },
                new[] { "SceneBind", "Walk", "", "", "", "", "HeroA", "First" },
                new[] { "Scene", "SecondScene", "", "", "", "", "", "Second" },
                new[] { "SceneBind", "Walk", "", "", "", "", "HeroB", "Second" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.Groups, Has.Count.EqualTo(2));
            Assert.That(outcome.Groups[0].ScenePlan.Definition.SceneName, Is.EqualTo("FirstScene"));
            Assert.That(outcome.Groups[1].ScenePlan.Definition.SceneName, Is.EqualTo("SecondScene"));
            Assert.That(outcome.Groups[0].ScenePlan.Bindings[0].TrackName, Is.EqualTo("Walk"));
            Assert.That(outcome.Groups[1].ScenePlan.Bindings[0].TrackName, Is.EqualTo("Walk"));
        }

        [Test]
        public void RejectsDuplicateSceneBindOnlyWithinItsTimelineGroup()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath", "timeline" },
                new[] { "Scene", "FirstScene", "", "", "", "", "", "First" },
                new[] { "SceneBind", "Walk", "", "", "", "", "HeroA", "First" },
                new[] { "SceneBind", "Walk", "", "", "", "", "HeroB", "First" },
                new[] { "Scene", "SecondScene", "", "", "", "", "", "Second" },
                new[] { "SceneBind", "Walk", "", "", "", "", "HeroC", "Second" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Has.Count.EqualTo(1));
            Assert.That(outcome.Errors[0].LineNumber, Is.EqualTo(4));
            Assert.That(outcome.Errors[0].Message, Does.Contain("Walk"));
            Assert.That(outcome.Groups[1].ScenePlan.Bindings, Has.Count.EqualTo(1));
        }

        [Test]
        public void RejectsMoreThanOneSceneWithinTheSameTimelineGroup()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath", "timeline" },
                new[] { "Scene", "FirstScene", "", "", "", "", "", "First" },
                new[] { "Scene", "DuplicateScene", "", "", "", "", "", "First" },
                new[] { "Scene", "SecondScene", "", "", "", "", "", "Second" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Has.Count.EqualTo(1));
            Assert.That(outcome.Errors[0].LineNumber, Is.EqualTo(3));
            Assert.That(outcome.Errors[0].Message, Does.Contain("同一 Timeline グループ内"));
        }

        [Test]
        public void RejectsSceneRelatedRowsWithoutASceneWithinTheirTimelineGroup()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath", "timeline" },
                new[] { "Scene", "FirstScene", "", "", "", "", "", "First" },
                new[] { "ScenePrefab", "", "", "", "", "", "Assets/Hero.prefab", "Second" },
                new[] { "SceneBind", "Walk", "", "", "", "", "Hero", "Second" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors.Select(error => error.LineNumber),
                Is.EquivalentTo(new int?[] { 3, 4 }));
            Assert.That(outcome.Errors.All(error => error.Message.Contains("Scene 行が必要です")), Is.True);
        }

        [Test]
        public void NormalizesPathLikeTimelineNamesToLastSegmentAndStripsKnownExtensions()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath", "timeline" },
                new[] { "Audio", "A", "Clip", "0", "0", "1", "Assets/a.wav", "Assets/Timelines/Main" },
                new[] { "Audio", "B", "Clip", "0", "0", "1", "Assets/b.wav", @"Assets\Timelines\Sub.playable" },
                new[] { "Audio", "C", "Clip", "0", "0", "1", "Assets/c.wav", "Main.playable" },
                new[] { "Audio", "D", "Clip", "0", "0", "1", "Assets/d.wav", "Ver1.5" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Is.Empty);
            Assert.That(outcome.Groups.Select(group => group.TimelineName),
                Is.EqualTo(new[] { "Main", "Sub", "Ver1.5" }));
            Assert.That(outcome.Groups[0].Rows.Select(row => row.TrackName), Is.EqualTo(new[] { "A", "C" }));
        }

        [Test]
        public void NormalizesSceneNamesAndRejectsValuesThatBecomeEmptyAfterNormalization()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath", "timeline" },
                new[] { "Scene", "Scenes/Stage1.unity", "", "", "", "", "", "Main" },
                new[] { "Audio", "A", "Clip", "0", "0", "1", "Assets/a.wav", "Main" },
                new[] { "Audio", "B", "Clip", "0", "0", "1", "Assets/b.wav", "Assets/" }
            };

            var outcome = CreateParser().Parse(rows);

            Assert.That(outcome.Errors, Has.Count.EqualTo(1));
            Assert.That(outcome.Errors[0].LineNumber, Is.EqualTo(4));
            Assert.That(outcome.Errors[0].Message, Does.Contain("Assets/"));
            Assert.That(outcome.Groups[0].ScenePlan.Definition.SceneName, Is.EqualTo("Stage1"));
        }
    }
}
