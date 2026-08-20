using System.Collections.Generic;
using NUnit.Framework;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class BuildModelTests
    {
        [Test]
        public void ClipRowRetainsTypedInputAsImmutableProperties()
        {
            var row = new ClipRow(7, "Audio", "BGM", "intro", 0.5, 0.25, 3.2, "Assets/Audio/intro.wav");

            Assert.That(row.LineNumber, Is.EqualTo(7));
            Assert.That(row.TrackType, Is.EqualTo("Audio"));
            Assert.That(row.TrackName, Is.EqualTo("BGM"));
            Assert.That(row.ClipName, Is.EqualTo("intro"));
            Assert.That(row.StartTime, Is.EqualTo(0.5));
            Assert.That(row.ClipIn, Is.EqualTo(0.25));
            Assert.That(row.Duration, Is.EqualTo(3.2));
            Assert.That(row.ResourcePath, Is.EqualTo("Assets/Audio/intro.wav"));
        }

        [Test]
        public void BuildResultCopiesErrorsAndExposesOutputPaths()
        {
            var error = new BuildError(BuildErrorCode.RowValidationError, 3, "sheet.csv", "Invalid duration");
            var errors = new List<BuildError> { error };
            var result = new BuildResult(false, null, null, errors);
            errors.Clear();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Is.SameAs(error));
            Assert.That(result.TimelineAssetPath, Is.Null);
            Assert.That(result.PrefabPath, Is.Null);
        }

        [Test]
        public void ErrorCodeContainsEveryDefinedFailureCategory()
        {
            var expected = new[]
            {
                "ArgumentInvalid", "SheetNotFound", "SheetParseError", "RowValidationError",
                "UnknownTrackType", "ResourceNotFound", "ResourceTypeMismatch", "ImportFailed",
                "OutputWriteFailed", "Unexpected"
            };

            CollectionAssert.AreEquivalent(expected, System.Enum.GetNames(typeof(BuildErrorCode)));
        }

        [Test]
        public void BuildRequestSupportsRequiredAndOptionalInputFields()
        {
            var request = new BuildRequest
            {
                SheetPath = "sheet.csv",
                OutputDirectory = "Assets/Generated",
                AssetName = "Timeline",
                ImportDirectory = "Assets/Imported"
            };

            Assert.That(request.SheetPath, Is.EqualTo("sheet.csv"));
            Assert.That(request.OutputDirectory, Is.EqualTo("Assets/Generated"));
            Assert.That(request.AssetName, Is.EqualTo("Timeline"));
            Assert.That(request.ImportDirectory, Is.EqualTo("Assets/Imported"));
        }
    }
}
