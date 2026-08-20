using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>AnimationClip リソースを解決するリゾルバです。</summary>
    internal sealed class AnimationClipResolver : IResourceResolver
    {
        private const string AssetsPrefix = "Assets/";
        private const string PreviewMarker = "__preview__";

        public string ResourceKind => "Animation";
        public Type AssetType => typeof(AnimationClip);

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
                    "AnimationClip のリソースパスが空です。");
                return false;
            }

            if (!IsSupportedExtension(resourcePath))
            {
                error = CreateError(BuildErrorCode.ResourceTypeMismatch, row,
                    "AnimationClip は .anim または .fbx のみ解決できます: " + row.ResourcePath);
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
                        importError == null
                            ? "AnimationClip のインポートに失敗しました。"
                            : importError.Message);
                    return false;
                }
            }

            assetPath = NormalizePath(assetPath);
            if (string.Equals(Path.GetExtension(assetPath), ".anim", StringComparison.OrdinalIgnoreCase))
            {
                var animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (animationClip != null)
                {
                    WarnIfLegacy(animationClip, assetPath);
                    asset = animationClip;
                    return true;
                }

                return FailForMissingOrWrongType(assetPath, row, out error);
            }

            var candidates = GetAnimationCandidates(assetPath);
            if (candidates.Count == 1)
            {
                WarnIfLegacy(candidates[0], assetPath);
                asset = candidates[0];
                return true;
            }

            if (candidates.Count > 1)
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    if (!string.Equals(candidates[i].name, row.ClipName, StringComparison.Ordinal))
                        continue;

                    WarnIfLegacy(candidates[i], assetPath);
                    asset = candidates[i];
                    return true;
                }

                error = CreateError(BuildErrorCode.ResourceNotFound, row,
                    "FBX 内の AnimationClip が clipName と一致しません: " + row.ClipName
                    + ". 候補: " + string.Join(", ", GetNames(candidates)));
                return false;
            }

            return FailForMissingOrWrongType(assetPath, row, out error);
        }

        private static List<AnimationClip> GetAnimationCandidates(string assetPath)
        {
            var candidates = new List<AnimationClip>();
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < assets.Length; i++)
            {
                var animationClip = assets[i] as AnimationClip;
                if (animationClip == null || animationClip.name.IndexOf(PreviewMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                candidates.Add(animationClip);
            }

            return candidates;
        }

        private static IEnumerable<string> GetNames(IReadOnlyList<AnimationClip> candidates)
        {
            var names = new string[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
                names[i] = candidates[i].name;
            return names;
        }

        private static bool FailForMissingOrWrongType(string assetPath, ClipRow row, out BuildError error)
        {
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            error = CreateError(
                mainAsset == null ? BuildErrorCode.ResourceNotFound : BuildErrorCode.ResourceTypeMismatch,
                row,
                mainAsset == null
                    ? "AnimationClip が見つかりません: " + row.ResourcePath
                    : "リソースは AnimationClip ではありません: " + row.ResourcePath);
            return false;
        }

        private static void WarnIfLegacy(AnimationClip animationClip, string assetPath)
        {
            if (animationClip.legacy)
                Debug.LogWarning("[UnityTimelineBuilder] legacy AnimationClip を使用します: " + assetPath + "/" + animationClip.name);
        }

        private static bool IsProjectAssetPath(string path)
        {
            return path.StartsWith(AssetsPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupportedExtension(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".anim", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase);
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
