using System.IO;
using Hidano.UnityTimelineBuilder.Editor;
using UnityEditor;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Samples.Editor
{
    /// <summary>TimelineBuilder.Build() をメニューから実行するサンプル。</summary>
    public static class TimelineBuildMenu
    {
        private const string OutputDirectory = "Assets/UnityTimelineBuilder/Generated";
        private const string LastSheetDirectoryKey =
            "Hidano.UnityTimelineBuilder.TimelineBuildMenu.LastSheetDirectory";

        [MenuItem("Tools/Hidano/Unity Timeline Builder/Build From Sheet...")]
        public static void BuildFromSheet()
        {
            var initialDirectory = EditorPrefs.GetString(LastSheetDirectoryKey, "Assets");
            if (!Directory.Exists(initialDirectory))
                initialDirectory = "Assets";

            var sheetPath = EditorUtility.OpenFilePanelWithFilters(
                "構築シートを選択", initialDirectory, new[] { "Sheet", "csv,tsv" });
            if (string.IsNullOrEmpty(sheetPath))
                return;

            var sheetDirectory = Path.GetDirectoryName(sheetPath);
            if (!string.IsNullOrEmpty(sheetDirectory))
                EditorPrefs.SetString(LastSheetDirectoryKey, sheetDirectory);

            var result = TimelineBuilder.Build(sheetPath, OutputDirectory);
            if (result.Success)
            {
                Debug.Log("[TimelineBuildMenu] 生成完了: "
                    + result.TimelineAssetPath + " / " + result.PrefabPath);
                return;
            }

            foreach (var error in result.Errors)
                Debug.LogError("[TimelineBuildMenu] " + error.Code
                    + " (行 " + error.LineNumber + "): " + error.Message);
        }
    }
}
