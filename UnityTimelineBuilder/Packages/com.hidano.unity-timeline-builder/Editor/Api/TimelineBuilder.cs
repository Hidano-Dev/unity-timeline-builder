using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>CSV/TSV から TimelineAsset と Prefab を構築する公開ファサード。</summary>
    public static class TimelineBuilder
    {
        private const string DefaultImportDirectory = "Assets/UnityTimelineBuilder/Imported";

        /// <summary>指定された構築要求を実行する。</summary>
        public static BuildResult Build(BuildRequest request)
        {
            ValidateRequest(request);

            var assetName = string.IsNullOrWhiteSpace(request.AssetName)
                ? Path.GetFileNameWithoutExtension(request.SheetPath)
                : request.AssetName.Trim();
            if (string.IsNullOrWhiteSpace(assetName))
                throw new ArgumentException("Asset name is required.", nameof(request));

            EnsureBuiltInResolvers();
            var timelinePath = CombineAssetPath(request.OutputDirectory, assetName + ".playable");
            var prefabPath = CombineAssetPath(request.OutputDirectory, assetName + ".prefab");
            var errors = new List<BuildError>();
            IReadOnlyList<IReadOnlyList<string>> rawRows;

            if (!File.Exists(request.SheetPath))
                return Failure(new BuildError(BuildErrorCode.SheetNotFound, null, request.SheetPath,
                    "構築情報ファイルが見つかりません: " + request.SheetPath));

            try
            {
                rawRows = new CsvSheetReader().ReadAll(request.SheetPath);
            }
            catch (SheetReadException exception)
            {
                return Failure(new BuildError(BuildErrorCode.SheetParseError, null, request.SheetPath,
                    exception.Message));
            }
            catch (Exception exception)
            {
                return Failure(new BuildError(BuildErrorCode.Unexpected, null, request.SheetPath,
                    exception.Message));
            }

            ParseOutcome parsed;
            try
            {
                var parser = new BuildSheetParser(TrackBuilderRegistry.IsKnownTrackType,
                    message => Debug.LogWarning("[UnityTimelineBuilder] " + message));
                parsed = parser.Parse(rawRows);
                errors.AddRange(parsed.Errors);
            }
            catch (Exception exception)
            {
                errors.Add(Unexpected(request.SheetPath, exception));
                return Failure(errors);
            }

            var resolvedRows = new List<ResolvedClipRow>();
            var context = new ResolveContext(request.ImportDirectory, Path.GetDirectoryName(request.SheetPath));
            foreach (var row in parsed.Rows)
            {
                try
                {
                    if (!TrackBuilderRegistry.TryGet(row.TrackType, out var builder))
                    {
                        errors.Add(new BuildError(BuildErrorCode.UnknownTrackType, row.LineNumber, null,
                            "未対応のトラック種別です: " + row.TrackType));
                        continue;
                    }

                    if (!ResourceResolverRegistry.TryGet(builder.ResourceKind, out var resolver))
                    {
                        errors.Add(new BuildError(BuildErrorCode.ResourceNotFound, row.LineNumber, row.ResourcePath,
                            "リソースリゾルバが登録されていません: " + builder.ResourceKind));
                        continue;
                    }

                    if (!resolver.TryResolve(row, context, out var asset, out var error))
                    {
                        errors.Add(error ?? new BuildError(BuildErrorCode.ResourceNotFound, row.LineNumber,
                            row.ResourcePath, "リソースを解決できませんでした: " + row.ResourcePath));
                        continue;
                    }

                    if (asset == null || !resolver.AssetType.IsInstanceOfType(asset))
                    {
                        errors.Add(new BuildError(BuildErrorCode.ResourceTypeMismatch, row.LineNumber,
                            row.ResourcePath, "解決されたリソースの型が一致しません: " + row.ResourcePath));
                        continue;
                    }

                    resolvedRows.Add(new ResolvedClipRow(row, builder, asset));
                }
                catch (Exception exception)
                {
                    errors.Add(Unexpected(request.SheetPath, exception, row.LineNumber, row.ResourcePath));
                }
            }

            if (errors.Count > 0)
                return Failure(errors);

            try
            {
                EnsureOutputDirectory(request.OutputDirectory);
                var timeline = new TimelineAssetFactory().Create(resolvedRows, timelinePath);
                new PrefabFactory().Create(timeline, prefabPath, assetName);
                Debug.Log("[UnityTimelineBuilder] TimelineAsset: " + timelinePath);
                Debug.Log("[UnityTimelineBuilder] Prefab: " + prefabPath);
                return new BuildResult(true, timelinePath, prefabPath, Array.Empty<BuildError>());
            }
            catch (Exception exception)
            {
                return Failure(new BuildError(BuildErrorCode.OutputWriteFailed, null,
                    timelinePath, exception.Message));
            }
        }

        /// <summary>簡易オーバーロードで構築を実行する。</summary>
        public static BuildResult Build(string sheetPath, string outputDirectory, string assetName = null)
        {
            return Build(new BuildRequest
            {
                SheetPath = sheetPath,
                OutputDirectory = outputDirectory,
                AssetName = assetName,
                ImportDirectory = DefaultImportDirectory
            });
        }

        private static void ValidateRequest(BuildRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            RequireValue(request.SheetPath, nameof(request.SheetPath));
            RequireValue(request.OutputDirectory, nameof(request.OutputDirectory));
            ValidateAssetsPath(request.OutputDirectory, nameof(request.OutputDirectory));
            if (!string.IsNullOrWhiteSpace(request.ImportDirectory))
                ValidateAssetsPath(request.ImportDirectory, nameof(request.ImportDirectory));
            else
                request.ImportDirectory = DefaultImportDirectory;
        }

        private static void RequireValue(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(name + " is required.", name);
        }

        private static void ValidateAssetsPath(string path, string name)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            if (!string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase)
                && !normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(name + " must be under Assets/.", name);
        }

        private static string CombineAssetPath(string directory, string fileName)
        {
            return directory.Replace('\\', '/').TrimEnd('/') + "/" + fileName;
        }

        /// <summary>AssetDatabase.CreateAsset は親フォルダを作らないため、出力先を事前に用意する。</summary>
        private static void EnsureOutputDirectory(string outputDirectory)
        {
            var normalized = outputDirectory.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            Directory.CreateDirectory(Path.Combine(projectRoot, normalized));
            AssetDatabase.Refresh();
        }

        private static void EnsureBuiltInResolvers()
        {
            if (!ResourceResolverRegistry.TryGet("Audio", out _))
                ResourceResolverRegistry.Register(new AudioClipResolver());
            if (!ResourceResolverRegistry.TryGet("Animation", out _))
                ResourceResolverRegistry.Register(new AnimationClipResolver());
        }

        private static BuildResult Failure(BuildError error) => Failure(new[] { error });

        private static BuildResult Failure(IEnumerable<BuildError> errors)
        {
            var list = errors.Where(error => error != null).ToList();
            foreach (var error in list)
                Debug.LogError("[UnityTimelineBuilder] " + error.Code + ": " + error.Message);
            return new BuildResult(false, null, null, list);
        }

        private static BuildError Unexpected(string sourcePath, Exception exception, int? lineNumber = null,
            string resourcePath = null)
        {
            Debug.LogException(exception);
            return new BuildError(BuildErrorCode.Unexpected, lineNumber, resourcePath ?? sourcePath,
                "予期しないエラーが発生しました: " + exception.Message);
        }
    }
}
