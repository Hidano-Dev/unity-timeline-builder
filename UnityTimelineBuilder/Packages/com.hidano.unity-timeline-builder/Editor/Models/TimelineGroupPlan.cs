using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>グループ化済みの1 Timeline分の構築計画を表す不変モデル。</summary>
    internal sealed class TimelineGroupPlan
    {
        /// <summary>CSV記載のTimeline名。レガシー入力ではnull。</summary>
        public string TimelineName { get; }

        /// <summary>グループ内で最初に現れたCSV行の行番号。</summary>
        public int FirstLineNumber { get; }

        /// <summary>このグループに属するクリップ行。</summary>
        public IReadOnlyList<ClipRow> Rows { get; }

        /// <summary>このグループのScene構築計画。Scene行がなければnull。</summary>
        public SceneBuildPlan ScenePlan { get; }

        public TimelineGroupPlan(string timelineName, int firstLineNumber,
            IReadOnlyList<ClipRow> rows, SceneBuildPlan scenePlan)
        {
            TimelineName = timelineName;
            FirstLineNumber = firstLineNumber;
            Rows = Copy(rows);
            ScenePlan = scenePlan;
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
        {
            return new ReadOnlyCollection<T>(new List<T>(values ?? new T[0]));
        }
    }
}
