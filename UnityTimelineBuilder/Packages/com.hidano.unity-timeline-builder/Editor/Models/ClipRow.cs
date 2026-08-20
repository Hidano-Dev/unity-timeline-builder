namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>構築情報の 1 行を型付きで表現した不変データ。</summary>
    public sealed class ClipRow
    {
        public int LineNumber { get; }
        public string TrackType { get; }
        public string TrackName { get; }
        public string ClipName { get; }
        public double StartTime { get; }
        public double ClipIn { get; }
        public double Duration { get; }
        public string ResourcePath { get; }

        public ClipRow(int lineNumber, string trackType, string trackName, string clipName,
            double startTime, double clipIn, double duration, string resourcePath)
        {
            LineNumber = lineNumber;
            TrackType = trackType;
            TrackName = trackName;
            ClipName = clipName;
            StartTime = startTime;
            ClipIn = clipIn;
            Duration = duration;
            ResourcePath = resourcePath;
        }
    }
}
