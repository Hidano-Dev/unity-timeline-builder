using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>AudioClip リソースを解決するリゾルバです。</summary>
    internal sealed class AudioClipResolver : IResourceResolver
    {
        private const string AssetsPrefix = "Assets/";

        public string ResourceKind => "Audio";
        public Type AssetType => typeof(AudioClip);

        public bool TryResolve(ClipRow row, ResolveContext context,
            out UnityEngine.Object asset, out BuildError error)
        {
            asset = null;
            error = null;

            if (row == null)
            {
                error = CreateError(BuildErrorCode.ArgumentInvalid, null, null,
                    "クリップ行が null です。");
                return false;
            }

            if (context == null)
            {
                error = CreateError(BuildErrorCode.ArgumentInvalid, row,
                    "リソース解決コンテキストが null です。");
                return false;
            }

            var resourcePath = NormalizePath(row.ResourcePath);
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                error = CreateError(BuildErrorCode.ResourceNotFound, row,
                    "AudioClip のリソースパスが空です。");
                return false;
            }

            if (!IsSupportedExtension(resourcePath))
            {
                error = CreateError(BuildErrorCode.ResourceTypeMismatch, row,
                    "AudioClip は wav または mp3 のみ解決できます: " + row.ResourcePath);
                return false;
            }

            var assetPath = resourcePath;
            if (!IsProjectAssetPath(resourcePath))
            {
                var importer = new ExternalAssetImporter();
                if (!importer.TryImportToProject(resourcePath, context,
                    out assetPath, out var importError))
                {
                    error = CreateError(
                        importError == null ? BuildErrorCode.ImportFailed : importError.Code,
                        row,
                        importError == null ? "AudioClip のインポートに失敗しました。" : importError.Message);
                    return false;
                }
            }

            assetPath = NormalizePath(assetPath);
            var loadedAsset = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (loadedAsset != null)
            {
                asset = loadedAsset;
                return true;
            }

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            error = CreateError(
                mainAsset == null ? BuildErrorCode.ResourceNotFound : BuildErrorCode.ResourceTypeMismatch,
                row,
                mainAsset == null
                    ? "AudioClip が見つかりません: " + row.ResourcePath
                    : "リソースは AudioClip ではありません: " + row.ResourcePath);
            return false;
        }

        private static bool IsProjectAssetPath(string path)
        {
            return path.StartsWith(AssetsPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupportedExtension(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? path : path.Replace('\\', '/').Trim();
        }

        private static BuildError CreateError(BuildErrorCode code, ClipRow row, string message)
        {
            return CreateError(code, row == null ? (int?)null : row.LineNumber,
                row == null ? null : row.ResourcePath, message);
        }

        private static BuildError CreateError(BuildErrorCode code, int? lineNumber,
            string sourcePath, string message)
        {
            return new BuildError(code, lineNumber, sourcePath, message);
        }
    }
}
