using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>プロジェクト外のファイルを Assets 配下へコピーし、同期インポートします。</summary>
    internal sealed class ExternalAssetImporter
    {
        private const string MetaExtension = ".meta";

        public bool TryImportToProject(string externalPath, ResolveContext context,
            out string assetPath, out BuildError error)
        {
            assetPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(externalPath))
            {
                error = CreateError(externalPath, "外部ファイルパスが空です。");
                return false;
            }

            if (context == null)
            {
                error = CreateError(externalPath, "リソース解決コンテキストが null です。");
                return false;
            }

            try
            {
                var isAbsolutePath = Path.IsPathRooted(externalPath);
                var resolvedPath = isAbsolutePath
                    ? externalPath
                    : Path.Combine(context.SheetDirectory, externalPath);
                resolvedPath = Path.GetFullPath(resolvedPath);
                if (!File.Exists(resolvedPath))
                {
                    error = CreateError(externalPath, "外部ファイルが見つかりません: " + resolvedPath);
                    return false;
                }

                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var normalizedImportDirectory = context.ImportDirectory.Replace('/', Path.DirectorySeparatorChar);
                var sourceDirectoryKey = GetSourceDirectoryKey(resolvedPath, context.SheetDirectory,
                    projectRoot, isAbsolutePath);
                var destinationDirectory = Path.Combine(projectRoot, normalizedImportDirectory,
                    GetStableSourceDirectoryName(sourceDirectoryKey));
                Directory.CreateDirectory(destinationDirectory);

                var fileName = Path.GetFileName(resolvedPath);
                var destinationFile = Path.Combine(destinationDirectory, fileName);
                var destinationExists = File.Exists(destinationFile);
                File.Copy(resolvedPath, destinationFile, true);

                CopyCompanionMetaIfPresent(resolvedPath, destinationFile);

                assetPath = context.ImportDirectory + "/" + Path.GetFileName(destinationDirectory)
                    + "/" + fileName;
                if (destinationExists)
                    Debug.Log("[UnityTimelineBuilder] 外部アセットを上書きしました: " + assetPath);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                {
                    error = CreateError(externalPath, "コピーしたファイルをアセットとして読み込めません: " + assetPath);
                    assetPath = null;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = CreateError(externalPath, "外部ファイルのインポートに失敗しました: " + exception.Message);
                assetPath = null;
                return false;
            }
        }

        private static BuildError CreateError(string sourcePath, string message)
        {
            return new BuildError(BuildErrorCode.ImportFailed, null, sourcePath, message);
        }

        /// <summary>初回取り込み時のみ companion .meta を複製し、GUID 衝突を防ぎます。</summary>
        private static void CopyCompanionMetaIfPresent(string sourceFile, string destinationFile)
        {
            var sourceMetaFile = sourceFile + MetaExtension;
            if (!File.Exists(sourceMetaFile))
                return;

            var destinationMetaFile = destinationFile + MetaExtension;
            if (File.Exists(destinationMetaFile))
                return;

            var content = File.ReadAllText(sourceMetaFile);
            File.WriteAllText(destinationMetaFile,
                ReplaceOrAppendGuid(content, Guid.NewGuid().ToString("N")));
        }

        /// <summary>meta ファイルの GUID 行を置換し、無ければ末尾へ追加します。</summary>
        private static string ReplaceOrAppendGuid(string content, string guid)
        {
            const string marker = "guid: ";
            var start = content.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return string.IsNullOrEmpty(content)
                    ? marker + guid
                    : content + DetectNewLine(content) + marker + guid;

            var end = content.IndexOfAny(new[] { '\r', '\n' }, start);
            return end < 0
                ? content.Substring(0, start) + marker + guid
                : content.Substring(0, start) + marker + guid + content.Substring(end);
        }

        /// <summary>相対パスはシート基準、絶対パスは実パス基準で安定したディレクトリキーへ正規化します。</summary>
        private static string GetSourceDirectoryKey(string resolvedPath, string sheetDirectory,
            string projectRoot, bool isAbsolutePath)
        {
            if (isAbsolutePath)
                return (Path.GetDirectoryName(resolvedPath) ?? resolvedPath).Replace('\\', '/');

            var resolvedDirectory = Path.GetDirectoryName(resolvedPath) ?? resolvedPath;
            var baseDirectory = Path.GetFullPath(sheetDirectory ?? Environment.CurrentDirectory);
            var relativeResourceDirectory = Path.GetRelativePath(baseDirectory, resolvedDirectory)
                .Replace('\\', '/');
            var normalizedBaseDirectory = baseDirectory.Replace('\\', '/');
            var normalizedProjectRoot = Path.GetFullPath(projectRoot).Replace('\\', '/');
            var sheetScope = TryGetPathRelativeToRoot(normalizedBaseDirectory, normalizedProjectRoot)
                ?? normalizedBaseDirectory;
            return sheetScope + "|" + relativeResourceDirectory;
        }

        /// <summary>同一ソースディレクトリの関連ファイルがまとまる安定した取り込み先名を返します。</summary>
        private static string GetStableSourceDirectoryName(string sourceDirectoryKey)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceDirectoryKey));
                return "src_" + ToLowerHex(bytes, 12);
            }
        }

        /// <summary>既存テキストの改行コードを保ったまま追記するための区切り文字を返します。</summary>
        private static string DetectNewLine(string content)
        {
            if (content.IndexOf("\r\n", StringComparison.Ordinal) >= 0)
                return "\r\n";
            if (content.IndexOf('\n') >= 0)
                return "\n";
            return Environment.NewLine;
        }

        /// <summary>指定パスがルート配下にある場合のみ、ルートからの相対パスを返します。</summary>
        private static string TryGetPathRelativeToRoot(string path, string root)
        {
            if (string.Equals(path, root, StringComparison.Ordinal))
                return ".";
            if (!path.StartsWith(root + "/", StringComparison.Ordinal))
                return null;
            return path.Substring(root.Length + 1);
        }

        private static string ToLowerHex(byte[] bytes, int maxCharacters)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length && builder.Length + 2 <= maxCharacters; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }
}
