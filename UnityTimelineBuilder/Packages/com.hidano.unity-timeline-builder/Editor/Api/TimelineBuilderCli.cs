using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>Unity Editor のバッチモードから TimelineBuilder を実行する CLI エントリポイント。</summary>
    public static class TimelineBuilderCli
    {
        private const int SuccessExitCode = 0;
        private const int BuildFailureExitCode = 1;
        private const int ArgumentFailureExitCode = 2;

        /// <summary>-executeMethod から呼び出されるエントリポイント。</summary>
        public static void Build()
        {
            var exitCode = Run(Environment.GetCommandLineArgs());
            EditorApplication.Exit(exitCode);
        }

        /// <summary>CLI 引数を処理し、Editor を終了せずに exit code を返す。</summary>
        public static int Run(string[] args)
        {
            try
            {
                var request = ParseRequest(args);
                var result = TimelineBuilder.Build(request);
                if (result == null)
                {
                    LogError("構築結果が返されませんでした。");
                    return BuildFailureExitCode;
                }

                if (!result.Success)
                {
                    LogError("構築に失敗しました (エラー数: " + result.Errors.Count + ")。");
                    foreach (var error in result.Errors)
                    {
                        if (error == null)
                            continue;
                        LogError(FormatError(error));
                    }
                    return BuildFailureExitCode;
                }

                foreach (var output in result.Outputs)
                {
                    if (output == null)
                        continue;
                    if (!string.IsNullOrWhiteSpace(output.TimelineAssetPath))
                        Debug.Log("[UnityTimelineBuilder] TimelineAsset: " + output.TimelineAssetPath);
                    if (!string.IsNullOrWhiteSpace(output.PrefabPath))
                        Debug.Log("[UnityTimelineBuilder] Prefab: " + output.PrefabPath);
                    if (!string.IsNullOrWhiteSpace(output.ScenePath))
                        Debug.Log("[UnityTimelineBuilder] Scene: " + output.ScenePath);
                }
                return SuccessExitCode;
            }
            catch (ArgumentException exception)
            {
                LogError("引数が不正です: " + exception.Message);
                return ArgumentFailureExitCode;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                LogError("予期しないエラーが発生しました: " + exception.Message);
                return BuildFailureExitCode;
            }
        }

        private static BuildRequest ParseRequest(string[] args)
        {
            if (args == null)
                throw new ArgumentException("コマンドライン引数が指定されていません。", nameof(args));

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var supportedOptions = new HashSet<string>(StringComparer.Ordinal)
            {
                "-sheetPath",
                "-outputDir",
                "-assetName",
                "-importDir"
            };

            for (var index = 0; index < args.Length; index++)
            {
                var option = args[index];
                if (!supportedOptions.Contains(option))
                    continue;

                if (values.ContainsKey(option))
                    throw new ArgumentException("オプションを重複して指定できません: " + option);
                if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1])
                    || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException("オプションの値が指定されていません: " + option);

                values.Add(option, args[++index]);
            }

            var sheetPath = GetRequired(values, "-sheetPath");
            var outputDirectory = GetRequired(values, "-outputDir");
            return new BuildRequest
            {
                SheetPath = sheetPath,
                OutputDirectory = outputDirectory,
                AssetName = GetOptional(values, "-assetName"),
                ImportDirectory = GetOptional(values, "-importDir")
            };
        }

        private static string GetRequired(IReadOnlyDictionary<string, string> values, string option)
        {
            if (!values.TryGetValue(option, out var value) || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("必須オプションが指定されていません: " + option);
            return value;
        }

        private static string GetOptional(IReadOnlyDictionary<string, string> values, string option)
        {
            return values.TryGetValue(option, out var value) ? value : null;
        }

        private static string FormatError(BuildError error)
        {
            var line = error.LineNumber.HasValue ? " (行 " + error.LineNumber.Value + ")" : string.Empty;
            var path = string.IsNullOrWhiteSpace(error.SourcePath) ? string.Empty : " [" + error.SourcePath + "]";
            var timeline = string.IsNullOrWhiteSpace(error.TimelineName)
                ? string.Empty
                : " [timeline: " + error.TimelineName + "]";
            return error.Code + line + path + timeline + ": " + error.Message;
        }

        private static void LogError(string message)
        {
            Debug.LogError("[UnityTimelineBuilder] " + message);
        }
    }
}
