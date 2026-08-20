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
            Assert.That(outcome.Errors[0].Message, Does.Contain("clipName"));
        }
    }
}
