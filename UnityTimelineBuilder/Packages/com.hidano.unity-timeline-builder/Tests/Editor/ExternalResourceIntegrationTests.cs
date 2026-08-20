using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class ExternalResourceIntegrationTests
    {
        private const string FixtureDirectory = "Packages/com.hidano.unity-timeline-builder/Tests/Fixtures";
        private const string ImportDirectory = "Assets/UnityTimelineBuilder/Tests/ExternalImported";
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Tests/ExternalIntegrationOutput";
        private string _externalDirectory;

        [SetUp]
        public void SetUp()
        {
            _externalDirectory = Path.Combine(Path.GetTempPath(), "UnityTimelineBuilderExternal_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_externalDirectory);
            var wav = Path.Combine(_externalDirectory, "external.wav");
            File.WriteAllBytes(wav, CreateSilentWave(48000));
            RunFfmpeg("-y -i \"" + wav + "\" -codec:a libmp3lame -b:a 32k \"" +
                Path.Combine(_externalDirectory, "external.mp3") + "\"");
            File.Copy(GetProjectPath(FixtureDirectory + "/external-multiple-clips.fbx"),
                Path.Combine(_externalDirectory, "external-multiple-clips.fbx"));
            File.WriteAllText(Path.Combine(_externalDirectory, "external-resource-integration.csv"),
                File.ReadAllText(GetProjectPath(FixtureDirectory + "/external-resource-integration.csv")));
            File.WriteAllText(Path.Combine(_externalDirectory, "external-resource-mismatch.csv"),
                File.ReadAllText(GetProjectPath(FixtureDirectory + "/external-resource-mismatch.csv")));
            EnsureFolder(ImportDirectory);
            EnsureFolder(OutputDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(OutputDirectory);
            AssetDatabase.DeleteAsset(ImportDirectory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (Directory.Exists(_externalDirectory))
                Directory.Delete(_externalDirectory, true);
        }

        [Test]
        public void BuildsTimelineFromExternalWavMp3AndNamedFbxClip()
        {
            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = Path.Combine(_externalDirectory, "external-resource-integration.csv"),
                OutputDirectory = OutputDirectory,
                AssetName = "ExternalResourceIntegration",
                ImportDirectory = ImportDirectory
            });

            Assert.That(result.Success, Is.True, FormatErrors(result));
            Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(ImportDirectory + "/external.wav"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(ImportDirectory + "/external.mp3"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(ImportDirectory + "/external-multiple-clips.fbx"), Is.Not.Null);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(
                OutputDirectory + "/ExternalResourceIntegration.playable");
            Assert.That(timeline, Is.Not.Null);
            var audioTrack = timeline.GetOutputTracks().OfType<AudioTrack>().Single();
            Assert.That(audioTrack.GetClips().Count(), Is.EqualTo(2));
            Assert.That(((AudioPlayableAsset)audioTrack.GetClips().First().asset).clip, Is.Not.Null);
            Assert.That(((AudioPlayableAsset)audioTrack.GetClips().Last().asset).clip, Is.Not.Null);
            var animationClip = timeline.GetOutputTracks().OfType<AnimationTrack>().Single().GetClips().Single();
            Assert.That(((AnimationPlayableAsset)animationClip.asset).clip.name, Is.EqualTo("Walk"));
        }

        [Test]
        public void ReportsLineNumberWhenExternalFbxClipNameDoesNotMatch()
        {
            var result = TimelineBuilder.Build(new BuildRequest
            {
                SheetPath = Path.Combine(_externalDirectory, "external-resource-mismatch.csv"),
                OutputDirectory = OutputDirectory,
                AssetName = "ExternalResourceMismatch",
                ImportDirectory = ImportDirectory
            });

            Assert.That(result.Success, Is.False);
            var error = result.Errors.Single(error => error.Code == BuildErrorCode.ResourceNotFound);
            Assert.That(error.LineNumber, Is.EqualTo(2));
            Assert.That(error.SourcePath, Is.EqualTo("external-multiple-clips.fbx"));
            Assert.That(error.Message, Does.Contain("MissingClip"));
            Assert.That(AssetDatabase.LoadAssetAtPath<TimelineAsset>(
                OutputDirectory + "/ExternalResourceMismatch.playable"), Is.Null);
        }

        private static string FormatErrors(BuildResult result)
        {
            return result == null ? "Build returned null." : string.Join("\n", result.Errors.Select(error => error.Message));
        }

        private static string GetProjectPath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void RunFfmpeg(string arguments)
        {
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            }))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                    Assert.Fail("ffmpeg failed: " + process.StandardError.ReadToEnd());
            }
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
            const int sampleRate = 48000;
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
    }
}
