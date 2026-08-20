using System;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>リソース解決時に使用するプロジェクト環境情報です。</summary>
    public sealed class ResolveContext
    {
        public string ImportDirectory { get; }
        public string SheetDirectory { get; }

        public ResolveContext(string importDirectory = "Assets/UnityTimelineBuilder/Imported",
            string sheetDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(importDirectory))
                throw new ArgumentException("Import directory is required.", nameof(importDirectory));

            ImportDirectory = importDirectory.Replace('\\', '/').TrimEnd('/');
            SheetDirectory = string.IsNullOrWhiteSpace(sheetDirectory)
                ? Environment.CurrentDirectory
                : sheetDirectory;
        }
    }
}
