using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>プロジェクト外のファイルを Assets 配下へコピーし、同期インポートします。</summary>
    public sealed class ExternalAssetImporter
    {
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
                var resolvedPath = Path.IsPathRooted(externalPath)
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
                var destinationDirectory = Path.Combine(projectRoot, normalizedImportDirectory);
                Directory.CreateDirectory(destinationDirectory);

                var fileName = Path.GetFileName(resolvedPath);
                var destinationFile = Path.Combine(destinationDirectory, fileName);
                var destinationExists = File.Exists(destinationFile);
                File.Copy(resolvedPath, destinationFile, true);

                assetPath = context.ImportDirectory + "/" + fileName;
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
    }
}
