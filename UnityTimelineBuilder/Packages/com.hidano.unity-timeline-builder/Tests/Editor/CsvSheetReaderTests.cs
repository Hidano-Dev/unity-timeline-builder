using System;
using System.IO;
using NUnit.Framework;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class CsvSheetReaderTests
    {
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "UnityTimelineBuilderCsvTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }

        [Test]
        public void ReadAllSupportsBomMixedNewlinesQuotedFieldsAndEscapedQuotes()
        {
            var path = Path.Combine(temporaryDirectory, "sheet.csv");
            File.WriteAllText(path, "name,description,value\r\nalpha,\"line one\nline two\",\"say \"\"hello\"\"\"\n\nbeta,plain,42\r\n", new System.Text.UTF8Encoding(true));

            var rows = new CsvSheetReader().ReadAll(path);

            Assert.That(rows, Has.Count.EqualTo(3));
            Assert.That(rows[0], Is.EqualTo(new[] { "name", "description", "value" }));
            Assert.That(rows[1], Is.EqualTo(new[] { "alpha", "line one\nline two", "say \"hello\"" }));
            Assert.That(rows[2], Is.EqualTo(new[] { "beta", "plain", "42" }));
        }

        [Test]
        public void ReadAllUsesTabDelimiterForTsv()
        {
            var path = Path.Combine(temporaryDirectory, "sheet.tsv");
            File.WriteAllText(path, "a\tb\r\n\"x\ty\"\tvalue\n");

            var rows = new CsvSheetReader().ReadAll(path);

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[1], Is.EqualTo(new[] { "x\ty", "value" }));
        }

        [Test]
        public void ReadAllRejectsUnsupportedExtensionWithPath()
        {
            var path = Path.Combine(temporaryDirectory, "sheet.txt");
            File.WriteAllText(path, "a,b");

            var exception = Assert.Throws<SheetReadException>(() => new CsvSheetReader().ReadAll(path));

            Assert.That(exception.Message, Does.Contain(path));
        }

        [Test]
        public void ReadAllReportsMissingFileWithPath()
        {
            var path = Path.Combine(temporaryDirectory, "missing.csv");

            var exception = Assert.Throws<SheetReadException>(() => new CsvSheetReader().ReadAll(path));

            Assert.That(exception.Message, Does.Contain(path));
        }

        [Test]
        public void ReadAllRejectsUnclosedQuotedFieldWithPath()
        {
            var path = Path.Combine(temporaryDirectory, "invalid.csv");
            File.WriteAllText(path, "a,\"unclosed\n");

            var exception = Assert.Throws<SheetReadException>(() => new CsvSheetReader().ReadAll(path));

            Assert.That(exception.Message, Does.Contain(path));
        }
    }
}
