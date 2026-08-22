using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor
{
    internal sealed class ParseOutcome
    {
        public IReadOnlyList<TimelineGroupPlan> Groups { get; }
        public bool HasTimelineColumn { get; }
        public IReadOnlyList<ClipRow> Rows { get; }
        public IReadOnlyList<BuildError> Errors { get; }
        public string WarningMessage { get; }
        public SceneBuildPlan ScenePlan { get; }

        internal ParseOutcome(IReadOnlyList<TimelineGroupPlan> groups, IReadOnlyList<BuildError> errors,
            string warningMessage, bool hasTimelineColumn)
        {
            Groups = groups;
            Errors = errors;
            WarningMessage = warningMessage;
            HasTimelineColumn = hasTimelineColumn;
            Rows = groups.Count == 0 ? Array.Empty<ClipRow>() : groups[0].Rows;
            ScenePlan = groups.Count == 0 ? null : groups[0].ScenePlan;
        }

        // Kept temporarily for callers being migrated to the group-based outcome.
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
            var groupBuilders = new List<GroupBuilder>();
            var groupsByName = new Dictionary<string, GroupBuilder>(StringComparer.Ordinal);
            GroupBuilder legacyGroup = null;
            var hasHeader = rawRows.Count > 0 && rawRows[0].Any(IsTrackTypeHeader);
            var columnIndexes = hasHeader ? MapHeader(rawRows[0], errors) : DefaultColumnIndexes();
            var hasTimelineColumn = hasHeader && columnIndexes.ContainsKey("timeline");
            var warning = hasHeader ? null : "ヘッダー未検出のため既定列順で解釈します。";

            if (!hasHeader)
                warningLogger(warning);
            if (errors.Count > 0)
                return new ParseOutcome(Array.Empty<TimelineGroupPlan>(), errors.AsReadOnly(), warning,
                    hasTimelineColumn);

            var firstDataRow = hasHeader ? 1 : 0;
            for (var rowIndex = firstDataRow; rowIndex < rawRows.Count; rowIndex++)
            {
                var lineNumber = rowIndex + 1;
                var fields = rawRows[rowIndex] ?? Array.Empty<string>();
                var timelineName = hasTimelineColumn
                    ? NormalizeAssetName(GetValue(fields, columnIndexes, "timeline"))
                    : null;
                GroupBuilder group;
                if (hasTimelineColumn)
                {
                    if (!TryGetValue(fields, columnIndexes, "timeline", out var timelineValue))
                    {
                        AddRangeError(lineNumber, "timeline は必須です", errors);
                    }
                    else if (timelineName.Length == 0 || !IsValidAssetFileName(timelineName))
                    {
                        AddRangeError(lineNumber, "Timeline 名がファイル名として不正です: " + timelineValue.Trim(), errors);
                    }

                    if (!groupsByName.TryGetValue(timelineName, out group))
                    {
                        group = new GroupBuilder(timelineName, lineNumber);
                        groupsByName.Add(timelineName, group);
                        groupBuilders.Add(group);
                    }
                }
                else
                {
                    if (legacyGroup == null)
                    {
                        legacyGroup = new GroupBuilder(null, lineNumber);
                        groupBuilders.Add(legacyGroup);
                    }
                    group = legacyGroup;
                }
                if (TryGetValue(fields, columnIndexes, "trackType", out var trackType) &&
                    IsSceneRowType(trackType))
                {
                    ParseSceneRow(fields, lineNumber, columnIndexes, trackType,
                        group, errors);
                }
                else
                {
                    ParseRow(fields, lineNumber, columnIndexes, group.Rows, errors);
                }
            }

            var plans = new List<TimelineGroupPlan>();
            foreach (var group in groupBuilders)
            {
                ValidateSceneRows(group.SceneDefinition, group.ScenePrefabs, group.SceneBindings, errors);
                var scenePlan = group.SceneDefinition == null
                    ? null
                    : new SceneBuildPlan(group.SceneDefinition, group.ScenePrefabs.AsReadOnly(),
                        group.SceneBindings.AsReadOnly());
                plans.Add(new TimelineGroupPlan(group.TimelineName, group.FirstLineNumber,
                    group.Rows.AsReadOnly(), scenePlan));
            }
            return new ParseOutcome(plans.AsReadOnly(), errors.AsReadOnly(), warning, hasTimelineColumn);
        }

        private static void ParseSceneRow(IReadOnlyList<string> fields, int lineNumber,
            IReadOnlyDictionary<string, int> indexes, string trackType,
            GroupBuilder group, List<BuildError> errors)
        {
            var definition = group.SceneDefinition;
            switch (trackType.Trim().ToLowerInvariant())
            {
                case "scene":
                    var sceneName = NormalizeAssetName(GetValue(fields, indexes, "trackName"));
                    if (!TryGetValue(fields, indexes, "trackName", out var rawSceneName))
                        AddRangeError(lineNumber, "Scene 行の必須値がありません: trackName (Scene 名)", errors);
                    else if (sceneName.Length == 0 || !IsValidAssetFileName(sceneName))
                        AddRangeError(lineNumber, "Scene 名がファイル名として不正です: " + rawSceneName.Trim(), errors);

                    if (definition != null)
                    {
                        AddRangeError(lineNumber, "同一 Timeline グループ内の Scene 行は 1 行のみ指定できます。", errors);
                        break;
                    }

                    group.SceneDefinition = new SceneDefinitionRow(lineNumber, sceneName,
                        StripSurroundingQuotes(GetValue(fields, indexes, "resourcePath")));
                    break;
                case "sceneprefab":
                    if (!TryGetValue(fields, indexes, "resourcePath", out _))
                        AddRangeError(lineNumber, "ScenePrefab 行の必須値がありません: resourcePath (Prefab)", errors);
                    group.ScenePrefabs.Add(new ScenePrefabRow(lineNumber,
                        StripSurroundingQuotes(GetValue(fields, indexes, "resourcePath"))));
                    break;
                case "scenebind":
                    var bindTrackName = GetValue(fields, indexes, "trackName");
                    var gameObjectName = StripSurroundingQuotes(GetValue(fields, indexes, "resourcePath"));
                    if (!TryGetValue(fields, indexes, "trackName", out _))
                        AddRangeError(lineNumber, "SceneBind 行の必須値がありません: trackName (Track 名)", errors);
                    if (!TryGetValue(fields, indexes, "resourcePath", out _))
                        AddRangeError(lineNumber, "SceneBind 行の必須値がありません: resourcePath (GameObject 名)", errors);
                    group.SceneBindings.Add(new SceneBindRow(lineNumber,
                        bindTrackName, gameObjectName));
                    break;
            }
        }

        private static void ValidateSceneRows(SceneDefinitionRow definition,
            List<ScenePrefabRow> prefabs, List<SceneBindRow> bindings, List<BuildError> errors)
        {
            if (definition == null)
            {
                foreach (var prefab in prefabs)
                    AddRangeError(prefab.LineNumber, "ScenePrefab 行には Scene 行が必要です。", errors);
                foreach (var binding in bindings)
                    AddRangeError(binding.LineNumber, "SceneBind 行には Scene 行が必要です。", errors);
            }

            var trackNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in bindings)
            {
                if (!string.IsNullOrEmpty(binding.TrackName) && !trackNames.Add(binding.TrackName))
                    AddRangeError(binding.LineNumber, "同一 Track 名への SceneBind 指定が重複しています: " + binding.TrackName, errors);
            }
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

        private static readonly string[] KnownAssetExtensions = { ".playable", ".prefab", ".unity", ".asset", ".csv" };

        /// <summary>パス様の入力は最後の区切り文字（/ または \）以降を採用し、既知の拡張子を除去する。
        /// ドットを含む名前（例: "Ver1.5"）は既知の拡張子でない限り保持する。</summary>
        internal static string NormalizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value ?? string.Empty;

            var name = value.Trim();
            var separatorIndex = name.LastIndexOfAny(new[] { '/', '\\' });
            if (separatorIndex >= 0)
                name = name.Substring(separatorIndex + 1);

            foreach (var extension in KnownAssetExtensions)
            {
                if (name.Length > extension.Length &&
                    name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - extension.Length);
                    break;
                }
            }
            return name.Trim();
        }
        private static bool IsValidAssetFileName(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "." || value == ".." || value[value.Length - 1] == '.' || value[value.Length - 1] == ' ')
                return false;

            foreach (var character in value)
            {
                if (character < 32 || "<>:\"/\\|?*".IndexOf(character) >= 0)
                    return false;
            }
            return true;
        }
        private static bool IsTrackTypeHeader(string value) => string.Equals((value ?? string.Empty).Trim(), "trackType", StringComparison.OrdinalIgnoreCase);
        private static bool IsSceneRowType(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            return string.Equals(normalized, "Scene", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "ScenePrefab", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "SceneBind", StringComparison.OrdinalIgnoreCase);
        }
        private static Dictionary<string, int> DefaultColumnIndexes() => ColumnOrder.Select((name, index) => new { name, index }).ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);

        private sealed class GroupBuilder
        {
            public readonly string TimelineName;
            public readonly int FirstLineNumber;
            public readonly List<ClipRow> Rows = new List<ClipRow>();
            public readonly List<ScenePrefabRow> ScenePrefabs = new List<ScenePrefabRow>();
            public readonly List<SceneBindRow> SceneBindings = new List<SceneBindRow>();
            public SceneDefinitionRow SceneDefinition;

            public GroupBuilder(string timelineName, int firstLineNumber)
            {
                TimelineName = timelineName;
                FirstLineNumber = firstLineNumber;
            }
        }
    }
}
