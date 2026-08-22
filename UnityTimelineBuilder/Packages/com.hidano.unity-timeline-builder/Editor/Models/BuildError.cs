namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>構築処理で報告するエラーの分類。</summary>
    public enum BuildErrorCode
    {
        ArgumentInvalid,
        SheetNotFound,
        SheetParseError,
        RowValidationError,
        UnknownTrackType,
        ResourceNotFound,
        ResourceTypeMismatch,
        ImportFailed,
        OutputWriteFailed,
        Unexpected,
        SceneTimelineNotFound,
        ScenePrefabInvalid,
        BindTrackNotFound,
        BindTargetNotFound,
        BindTargetDuplicated,
        BindTargetMissingAnimator,
        SceneWriteFailed,
        SceneBuildCanceled,
        BindTrackDuplicated,
        AssetNameConflict
    }

    /// <summary>構築エラーの詳細。該当しない行番号・パスは null または空文字になる。</summary>
    public sealed class BuildError
    {
        public BuildErrorCode Code { get; }
        public int? LineNumber { get; }
        public string SourcePath { get; }
        public string Message { get; }
        public string TimelineName { get; }

        public BuildError(BuildErrorCode code, int? lineNumber, string sourcePath, string message)
            : this(code, lineNumber, sourcePath, message, null)
        {
        }

        public BuildError(BuildErrorCode code, int? lineNumber, string sourcePath, string message,
            string timelineName)
        {
            Code = code;
            LineNumber = lineNumber;
            SourcePath = sourcePath;
            Message = message;
            TimelineName = timelineName;
        }
    }
}
