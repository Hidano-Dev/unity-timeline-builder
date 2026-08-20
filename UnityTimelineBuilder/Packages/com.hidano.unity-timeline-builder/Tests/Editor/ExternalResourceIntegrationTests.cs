using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class ExternalResourceIntegrationTests
    {
        private string _sourceDirectory;
        private string _sheetPath;
        private string _importDirectory;
        private string _outputDirectory;

        [SetUp]
        public void SetUp()
        {
            _sourceDirectory = Path.Combine(Path.GetTempPath(), "UnityTimelineBuilderExternal_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_sourceDirectory);
            _sheetPath = Path.Combine(_sourceDirectory, "external.csv");
            _importDirectory = "Assets/UnityTimelineBuilder/Tests/ExternalImported_" + Guid.NewGuid().ToString("N");
            _outputDirectory = "Assets/UnityTimelineBuilder/Tests/ExternalOutput_" + Guid.NewGuid().ToString("N");
            EnsureFolder(_outputDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(_outputDirectory);
            AssetDatabase.DeleteAsset(_importDirectory);
            AssetDatabase.Refresh();
            if (Directory.Exists(_sourceDirectory))
                Directory.Delete(_sourceDirectory, true);
        }

        [Test]
        public void BuildsFromExternalWaveAndCopiesItToImportDirectory()
        {
            var wavePath = Path.Combine(_sourceDirectory, "external.wav");
            File.WriteAllBytes(wavePath, CreateSilentWave(800));
            WriteSheet("Audio,External,Sound,0,0,1," + wavePath);

            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = _sheetPath,
                OutputDirectory = _outputDirectory,
                AssetName = "ExternalAudio",
                ImportDirectory = _importDirectory
            });

            Assert.That(result.Success, Is.True, FormatErrors(result));
            var importedPath = _importDirectory + "/external.wav";
            var imported = AssetDatabase.LoadAssetAtPath<AudioClip>(importedPath);
            Assert.That(imported, Is.Not.Null);
            Assert.That(File.Exists(ProjectPath(importedPath)), Is.True);
        }

        [Test]
        public void ReportsLineAndSourceWhenExternalFbxClipNameCannotBeResolved()
        {
            var fbxPath = Path.Combine(_sourceDirectory, "multiple-clips.fbx");
            File.WriteAllText(fbxPath, MinimalFbxFixture);
            WriteSheet("Audio,External,Sound,0,0,1," + CreateUnusedWave() + "\n"
                + "Animation,Character,MissingClip,1,0,1," + fbxPath);
            LogAssert.Expect(LogType.Error, new Regex(".*ResourceTypeMismatch.*multiple-clips\\.fbx.*"));

            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = _sheetPath,
                OutputDirectory = _outputDirectory,
                AssetName = "ExternalAnimation",
                ImportDirectory = _importDirectory
            });

            Assert.That(result.Success, Is.False);
            var error = result.Errors.Single(error => error.LineNumber == 3);
            Assert.That(error.Code, Is.EqualTo(BuildErrorCode.ResourceNotFound).Or.EqualTo(BuildErrorCode.ResourceTypeMismatch));
            Assert.That(error.LineNumber, Is.EqualTo(3));
            Assert.That(error.SourcePath, Is.EqualTo(fbxPath));
            Assert.That(File.Exists(ProjectPath(_importDirectory + "/multiple-clips.fbx")), Is.True);
        }

        [Test]
        public void BuildsFromExternalMp3AndCopiesItToImportDirectory()
        {
            var mp3Path = Path.Combine(_sourceDirectory, "external.mp3");
            File.WriteAllBytes(mp3Path, CreateSilentWave(800));
            WriteSheet("Audio,External,Sound,0,0,1," + mp3Path);

            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = _sheetPath,
                OutputDirectory = _outputDirectory,
                AssetName = "ExternalMp3",
                ImportDirectory = _importDirectory
            });

            Assert.That(result.Success, Is.True, FormatErrors(result));
            Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(_importDirectory + "/external.mp3"), Is.Not.Null);
            Assert.That(File.Exists(ProjectPath(_importDirectory + "/external.mp3")), Is.True);
        }

        private void WriteSheet(string rows)
        {
            File.WriteAllText(_sheetPath,
                "trackType,trackName,clipName,startTime,clipIn,duration,resourcePath\n" + rows + "\n");
        }

        private string CreateUnusedWave()
        {
            var path = Path.Combine(_sourceDirectory, "external.wav");
            if (!File.Exists(path))
                File.WriteAllBytes(path, CreateSilentWave(800));
            return path;
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
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static byte[] CreateSilentWave(int sampleCount)
        {
            const short channels = 1;
            const short bitsPerSample = 16;
            const int sampleRate = 8000;
            var dataSize = sampleCount * channels * (bitsPerSample / 8);
            using (var stream = new MemoryStream(44 + dataSize))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * bitsPerSample / 8);
                writer.Write((short)(channels * bitsPerSample / 8));
                writer.Write(bitsPerSample);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                writer.Write(new byte[dataSize]);
                return stream.ToArray();
            }
        }

        private const string MinimalFbxFixture = @"; FBX 7.3.0 project file
FBXHeaderExtension:  {
    FBXHeaderVersion: 1003
    FBXVersion: 7300
    Creator: ""UnityTimelineBuilder test""
}
Definitions:  {
    Version: 100
    Count: 0
}
Objects:  {
}
Connections:  {
}";
    }
}
