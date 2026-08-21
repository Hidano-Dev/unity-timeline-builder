using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor
{
    internal sealed class ParseOutcome
    {
        public IReadOnlyList<ClipRow> Rows { get; }
        public IReadOnlyList<BuildError> Errors { get; }
        public string WarningMessage { get; }

        internal ParseOutcome(IReadOnlyList<ClipRow> rows, IReadOnlyList<BuildError> errors, string warningMessage)
        {
            Rows = rows;
            Errors = errors;
            WarningMessage = warningMessage;
        }
    }

    /// <summary>構築情報のヘッダー認識、列マッピング、行検証を行うパーサー。</summary>
    internal sealed class BuildSheetParser
    {
        private static readonly string[] ColumnOrder =
        {
            "trackType", "trackName", "clipName", "startTime", "clipIn", "duration", "resourcePath"
        };

        private static readonly string[] RequiredColumns =
        {
            "trackType", "trackName", "startTime", "clipIn", "resourcePath"
        };

        private readonly Func<string, bool> isKnownTrackType;
        private readonly Action<string> warningLogger;

        public BuildSheetParser(Func<string, bool> isKnownTrackType, Action<string> warningLogger = null)
        {
            this.isKnownTrackType = isKnownTrackType ?? throw new ArgumentNullException(nameof(isKnownTrackType));
            this.warningLogger = warningLogger ?? (message => Debug.LogWarning("[UnityTimelineBuilder] " + message));
        }

        public ParseOutcome Parse(IReadOnlyList<IReadOnlyList<string>> rawRows)
        {
            if (rawRows == null)
                throw new ArgumentNullException(nameof(rawRows));

            var errors = new List<BuildError>();
            var parsedRows = new List<ClipRow>();
            var hasHeader = rawRows.Count > 0 && rawRows[0].Any(IsTrackTypeHeader);
            var columnIndexes = hasHeader ? MapHeader(rawRows[0], errors) : DefaultColumnIndexes();
            var warning = hasHeader ? null : "ヘッダー未検出のため既定列順で解釈します。";

            if (!hasHeader)
                warningLogger(warning);
            if (errors.Count > 0)
                return new ParseOutcome(parsedRows.AsReadOnly(), errors.AsReadOnly(), warning);

            var firstDataRow = hasHeader ? 1 : 0;
            for (var rowIndex = firstDataRow; rowIndex < rawRows.Count; rowIndex++)
            {
                var lineNumber = rowIndex + 1;
                var fields = rawRows[rowIndex] ?? Array.Empty<string>();
                ParseRow(fields, lineNumber, columnIndexes, parsedRows, errors);
            }

            return new ParseOutcome(parsedRows.AsReadOnly(), errors.AsReadOnly(), warning);
        }

        private Dictionary<string, int> MapHeader(IReadOnlyList<string> header, List<BuildError> errors)
        {
            var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < header.Count; index++)
            {
                var name = (header[index] ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;
                if (indexes.ContainsKey(name))
                    errors.Add(new BuildError(BuildErrorCode.RowValidationError, 1, null, "ヘッダー列が重複しています: " + name));
                else
                    indexes[name] = index;
            }

            var missing = RequiredColumns.Where(column => !indexes.ContainsKey(column)).ToArray();
            if (missing.Length > 0)
                errors.Add(new BuildError(BuildErrorCode.RowValidationError, 1, null, "必須列がありません: " + string.Join(", ", missing)));
            return indexes;
        }

        private void ParseRow(IReadOnlyList<string> fields, int lineNumber, IReadOnlyDictionary<string, int> indexes,
            List<ClipRow> rows, List<BuildError> errors)
        {
            var missing = RequiredColumns.Where(column => !TryGetValue(fields, indexes, column, out _)).ToArray();
            if (missing.Length > 0)
            {
                errors.Add(new BuildError(BuildErrorCode.RowValidationError, lineNumber, null,
                    "必須値がありません: " + string.Join(", ", missing)));
                return;
            }

            var trackType = GetValue(fields, indexes, "trackType");
            var trackName = GetValue(fields, indexes, "trackName");
            var clipName = GetValue(fields, indexes, "clipName");
            var resourcePath = StripSurroundingQuotes(GetValue(fields, indexes, "resourcePath"));
            if (!isKnownTrackType(trackType))
                errors.Add(new BuildError(BuildErrorCode.UnknownTrackType, lineNumber, null, "未対応のトラック種別です: " + trackType));

            var valid = true;
            var startTime = ParseNumber(GetValue(fields, indexes, "startTime"), "startTime", lineNumber, errors, ref valid);
            var clipIn = ParseNumber(GetValue(fields, indexes, "clipIn"), "clipIn", lineNumber, errors, ref valid);
            double? duration = null;
            if (TryGetValue(fields, indexes, "duration", out var durationText))
            {
                duration = ParseNumber(durationText.Trim(), "duration", lineNumber, errors, ref valid);
                if (duration <= 0)
                {
                    AddRangeError(lineNumber, "duration は 0 より大きく指定してください。", errors);
                    valid = false;
                }
            }
            if (startTime < 0)
            {
                AddRangeError(lineNumber, "startTime は 0 以上で指定してください。", errors);
                valid = false;
            }
            if (clipIn < 0)
            {
                AddRangeError(lineNumber, "clipIn は 0 以上で指定してください。", errors);
                valid = false;
            }
            if (valid && isKnownTrackType(trackType))
                rows.Add(new ClipRow(lineNumber, trackType, trackName, clipName, startTime, clipIn, duration, resourcePath));
        }

        private static double ParseNumber(string value, string name, int lineNumber, List<BuildError> errors, ref bool valid)
        {
            double result;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) || double.IsNaN(result) || double.IsInfinity(result))
            {
                AddRangeError(lineNumber, "数値として解釈できません (" + name + "): " + value, errors);
                valid = false;
                return 0;
            }
            return result;
        }

        private static bool TryGetValue(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> indexes,
            string column, out string value)
        {
            value = null;
            if (!indexes.TryGetValue(column, out var index) || index < 0 || index >= fields.Count)
                return false;
            value = fields[index];
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string GetValue(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> indexes, string column)
            => TryGetValue(fields, indexes, column, out var value) ? value.Trim() : string.Empty;

        /// <summary>エクスプローラーの「パスのコピー」等で付くダブルクォート囲みを外す。</summary>
        private static string StripSurroundingQuotes(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2).Trim();
            return value;
        }

        private static void AddRangeError(int lineNumber, string message, List<BuildError> errors) => errors.Add(new BuildError(BuildErrorCode.RowValidationError, lineNumber, null, message));
        private static bool IsTrackTypeHeader(string value) => string.Equals((value ?? string.Empty).Trim(), "trackType", StringComparison.OrdinalIgnoreCase);
        private static Dictionary<string, int> DefaultColumnIndexes() => ColumnOrder.Select((name, index) => new { name, index }).ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
    }
}
