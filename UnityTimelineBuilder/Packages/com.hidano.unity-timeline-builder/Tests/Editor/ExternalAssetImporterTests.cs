using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class ExternalAssetImporterTests
    {
        private string _sourceDirectory;
        private string _importDirectory;

        [SetUp]
        public void SetUp()
        {
            _sourceDirectory = Path.Combine(Path.GetTempPath(), "UnityTimelineBuilderTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_sourceDirectory);
            _importDirectory = "Assets/UnityTimelineBuilder/Imported/Test_" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_sourceDirectory))
                Directory.Delete(_sourceDirectory, true);

            // Directory.Delete ではフォルダの .meta が残るため AssetDatabase 経由で削除する
            AssetDatabase.DeleteAsset(_importDirectory);
            AssetDatabase.Refresh();
        }

        [Test]
        public void CopiesAbsoluteFileAndSynchronouslyImportsIt()
        {
            var sourcePath = Path.Combine(_sourceDirectory, "sample.txt");
            File.WriteAllText(sourcePath, "first");
            var context = new ResolveContext(_importDirectory, _sourceDirectory);
            var importer = new ExternalAssetImporter();

            var imported = importer.TryImportToProject(sourcePath, context, out var assetPath, out var error);

            Assert.That(imported, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(assetPath, Is.EqualTo(_importDirectory + "/sample.txt"));
            Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath), Is.Not.Null);
            Assert.That(File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar))), Is.EqualTo("first"));
        }

        [Test]
        public void ResolvesRelativeFileFromSheetDirectoryAndOverwritesExistingCopy()
        {
            var sourcePath = Path.Combine(_sourceDirectory, "sample.txt");
            File.WriteAllText(sourcePath, "first");
            var context = new ResolveContext(_importDirectory, _sourceDirectory);
            var importer = new ExternalAssetImporter();

            Assert.That(importer.TryImportToProject("sample.txt", context, out _, out var firstError), Is.True);
            Assert.That(firstError, Is.Null);
            File.WriteAllText(sourcePath, "second");

            var imported = importer.TryImportToProject("sample.txt", context, out var assetPath, out var error);

            Assert.That(imported, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath).text, Is.EqualTo("second"));
        }

        [Test]
        public void ReturnsImportErrorWhenSourceDoesNotExist()
        {
            var importer = new ExternalAssetImporter();
            var imported = importer.TryImportToProject("missing.wav",
                new ResolveContext(_importDirectory, _sourceDirectory), out var assetPath, out var error);

            Assert.That(imported, Is.False);
            Assert.That(assetPath, Is.Null);
            Assert.That(error.Code, Is.EqualTo(BuildErrorCode.ImportFailed));
            Assert.That(error.SourcePath, Is.EqualTo("missing.wav"));
        }
    }
}
