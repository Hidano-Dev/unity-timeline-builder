using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor
{
    public static class TimelineBuilder
    {
        private const string DefaultImportDirectory = "Assets/UnityTimelineBuilder/Imported";

        public static BuildResult Build(BuildRequest request)
        {
            ValidateRequest(request);
            var fallbackAssetName = string.IsNullOrWhiteSpace(request.AssetName)
                ? Path.GetFileNameWithoutExtension(request.SheetPath) : request.AssetName.Trim();
            if (string.IsNullOrWhiteSpace(fallbackAssetName))
                throw new ArgumentException("Asset name is required.", nameof(request));

            EnsureBuiltInResolvers();
            if (!File.Exists(request.SheetPath))
                return Failure(new BuildError(BuildErrorCode.SheetNotFound, null, request.SheetPath,
                    "Sheet file was not found: " + request.SheetPath));

            IReadOnlyList<IReadOnlyList<string>> rawRows;
            try { rawRows = new CsvSheetReader().ReadAll(request.SheetPath); }
            catch (SheetReadException exception)
            { return Failure(new BuildError(BuildErrorCode.SheetParseError, null, request.SheetPath, exception.Message)); }
            catch (Exception exception)
            { return Failure(new BuildError(BuildErrorCode.Unexpected, null, request.SheetPath, exception.Message)); }

            ParseOutcome parsed;
            try
            {
                parsed = new BuildSheetParser(TrackBuilderRegistry.IsKnownTrackType,
                    message => Debug.LogWarning("[UnityTimelineBuilder] " + message)).Parse(rawRows);
            }
            catch (Exception exception)
            { return Failure(Unexpected(request.SheetPath, exception)); }

            var errors = new List<BuildError>(AnnotateErrors(parsed.Errors, parsed.Groups, request.SheetPath));
            if (parsed.HasTimelineColumn && !string.IsNullOrWhiteSpace(request.AssetName))
            {
                errors.Add(new BuildError(BuildErrorCode.AssetNameConflict, null, request.SheetPath,
                    "AssetName cannot be specified when the sheet contains a timeline column: " + request.AssetName.Trim()));
                return Failure(errors);
            }

            IReadOnlyList<PlannedGroupOutputs> plans;
            try { plans = new OutputPathPlanner().Plan(parsed.Groups, request.OutputDirectory, fallbackAssetName); }
            catch (Exception exception)
            { return Failure(errors.Concat(new[] { Unexpected(request.SheetPath, exception) })); }

            var resolvedByGroup = new List<IReadOnlyList<ResolvedClipRow>>();
            var sceneValidationByGroup = new List<SceneBuildValidationResult>();
            var context = new ResolveContext(request.ImportDirectory, Path.GetDirectoryName(request.SheetPath));
            for (var index = 0; index < parsed.Groups.Count; index++)
            {
                var group = parsed.Groups[index];
                var groupErrors = new List<BuildError>();
                var resolvedRows = ResolveRows(group.Rows, context, request.SheetPath, groupErrors);
                resolvedByGroup.Add(resolvedRows);

                SceneBuildValidationResult sceneValidation = null;
                if (group.ScenePlan != null)
                {
                    var plan = plans[index];
                    var validator = new SceneBuildValidator();
                    if (!validator.TryValidate(group.ScenePlan, group.Rows, plan.TimelineAssetPath,
                        plan.PrefabPath, request.OutputDirectory, out sceneValidation, out var sceneErrors))
                        groupErrors.AddRange(sceneErrors);
                }
                sceneValidationByGroup.Add(sceneValidation);
                errors.AddRange(AnnotateErrors(groupErrors, new[] { group }, request.SheetPath));
            }

            // This is the verification gate: all groups have been checked before any asset is written.
            if (errors.Count > 0)
                return Failure(errors);
            if (plans.Count == 0)
                return Failure(new BuildError(BuildErrorCode.RowValidationError, null, request.SheetPath,
                    "No build rows were found."));

            // Generation remains the single-group pipeline owned by task 4.3; this task only changes its preflight.
            var firstPlan = plans[0];
            var firstGroup = parsed.Groups[0];
            var timelinePath = firstPlan.TimelineAssetPath;
            var prefabPath = firstPlan.PrefabPath;
            try
            {
                EnsureOutputDirectory(request.OutputDirectory);
                var timeline = new TimelineAssetFactory().Create(resolvedByGroup[0], timelinePath);
                new PrefabFactory().Create(timeline, prefabPath, firstPlan.AssetName);
                Debug.Log("[UnityTimelineBuilder] TimelineAsset: " + timelinePath);
                Debug.Log("[UnityTimelineBuilder] Prefab: " + prefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(timelinePath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var persistedTimeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);

                if (firstGroup.ScenePlan == null)
                    return new BuildResult(true, timelinePath, prefabPath, Array.Empty<BuildError>());

                var sceneValidation = sceneValidationByGroup[0];
                var sceneTimeline = string.IsNullOrWhiteSpace(firstGroup.ScenePlan.Definition.TimelineAssetPath)
                    ? persistedTimeline : sceneValidation.Timeline;
                if (sceneTimeline == null)
                    return Failure(new BuildError(BuildErrorCode.SceneTimelineNotFound,
                        firstGroup.ScenePlan.Definition.LineNumber, timelinePath,
                        "Generated TimelineAsset was not found after creation."), timelinePath, prefabPath);

                var scenePath = firstPlan.ScenePath;
                var sceneContext = new SceneBuildContext(firstGroup.ScenePlan, sceneTimeline,
                    sceneValidation.PrefabAssets, scenePath, firstPlan.AssetName,
                    string.IsNullOrWhiteSpace(firstGroup.ScenePlan.Definition.TimelineAssetPath)
                        ? timelinePath : firstGroup.ScenePlan.Definition.TimelineAssetPath, prefabPath);
                if (!new SceneFactory().TryCreate(sceneContext, out var createdScenePath, out var sceneErrors))
                    return Failure(sceneErrors, timelinePath, prefabPath);
                Debug.Log("[UnityTimelineBuilder] Scene: " + createdScenePath);
                return new BuildResult(true, timelinePath, prefabPath, createdScenePath, Array.Empty<BuildError>());
            }
            catch (Exception exception)
            { return Failure(new BuildError(BuildErrorCode.OutputWriteFailed, null, timelinePath, exception.Message)); }
        }

        public static BuildResult Build(string sheetPath, string outputDirectory, string assetName = null)
        {
            return Build(new BuildRequest { SheetPath = sheetPath, OutputDirectory = outputDirectory,
                AssetName = assetName, ImportDirectory = DefaultImportDirectory });
        }

        private static List<ResolvedClipRow> ResolveRows(IReadOnlyList<ClipRow> rows, ResolveContext context,
            string sourcePath, List<BuildError> errors)
        {
            var resolved = new List<ResolvedClipRow>();
            foreach (var row in rows)
            {
                try
                {
                    if (!TrackBuilderRegistry.TryGet(row.TrackType, out var builder))
                    { errors.Add(new BuildError(BuildErrorCode.UnknownTrackType, row.LineNumber, null, "Unknown track type: " + row.TrackType)); continue; }
                    if (!ResourceResolverRegistry.TryGet(builder.ResourceKind, out var resolver))
                    { errors.Add(new BuildError(BuildErrorCode.ResourceNotFound, row.LineNumber, row.ResourcePath, "Resource resolver is not registered: " + builder.ResourceKind)); continue; }
                    if (!resolver.TryResolve(row, context, out var asset, out var error))
                    { errors.Add(error ?? new BuildError(BuildErrorCode.ResourceNotFound, row.LineNumber, row.ResourcePath, "Resource could not be resolved: " + row.ResourcePath)); continue; }
                    if (asset == null || !resolver.AssetType.IsInstanceOfType(asset))
                    { errors.Add(new BuildError(BuildErrorCode.ResourceTypeMismatch, row.LineNumber, row.ResourcePath, "Resolved resource type does not match: " + row.ResourcePath)); continue; }
                    resolved.Add(new ResolvedClipRow(row, builder, asset));
                }
                catch (Exception exception) { errors.Add(Unexpected(sourcePath, exception, row.LineNumber, row.ResourcePath)); }
            }
            return resolved;
        }

        private static IReadOnlyList<BuildError> AnnotateErrors(IEnumerable<BuildError> source,
            IReadOnlyList<TimelineGroupPlan> groups, string sourcePath)
        {
            var result = new List<BuildError>();
            foreach (var error in source ?? Enumerable.Empty<BuildError>())
            {
                if (error == null) continue;
                var group = groups.FirstOrDefault(candidate => candidate != null &&
                    (candidate.FirstLineNumber == error.LineNumber || candidate.Rows.Any(row => row.LineNumber == error.LineNumber) ||
                     (candidate.ScenePlan != null && candidate.ScenePlan.Definition.LineNumber == error.LineNumber) ||
                     (candidate.ScenePlan != null && candidate.ScenePlan.Prefabs.Any(row => row.LineNumber == error.LineNumber)) ||
                     (candidate.ScenePlan != null && candidate.ScenePlan.Bindings.Any(row => row.LineNumber == error.LineNumber))));
                result.Add(new BuildError(error.Code, error.LineNumber, error.SourcePath ?? sourcePath,
                    error.Message, error.TimelineName ?? (group == null ? null : group.TimelineName)));
            }
            return result;
        }

        private static void ValidateRequest(BuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireValue(request.SheetPath, nameof(request.SheetPath)); RequireValue(request.OutputDirectory, nameof(request.OutputDirectory));
            ValidateAssetsPath(request.OutputDirectory, nameof(request.OutputDirectory));
            if (!string.IsNullOrWhiteSpace(request.ImportDirectory)) ValidateAssetsPath(request.ImportDirectory, nameof(request.ImportDirectory));
            else request.ImportDirectory = DefaultImportDirectory;
        }
        private static void RequireValue(string value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required.", name); }
        private static void ValidateAssetsPath(string path, string name)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            if (!string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase) && !normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(name + " must be under Assets/.", name);
        }
        private static void EnsureOutputDirectory(string outputDirectory)
        {
            var normalized = outputDirectory.Replace('\\', '/').TrimEnd('/'); if (AssetDatabase.IsValidFolder(normalized)) return;
            var parts = normalized.Split('/'); var current = parts[0];
            for (var i = 1; i < parts.Length; i++) { var next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, parts[i]))) throw new InvalidOperationException("Failed to create output folder: " + next); current = next; }
        }
        private static void EnsureBuiltInResolvers()
        {
            if (!ResourceResolverRegistry.TryGet("Audio", out _)) ResourceResolverRegistry.Register(new AudioClipResolver());
            if (!ResourceResolverRegistry.TryGet("Animation", out _)) ResourceResolverRegistry.Register(new AnimationClipResolver());
        }
        private static BuildResult Failure(BuildError error) => Failure(new[] { error });
        private static BuildResult Failure(BuildError error, string timelinePath, string prefabPath) => Failure(new[] { error }, timelinePath, prefabPath);
        private static BuildResult Failure(IEnumerable<BuildError> errors) => Failure(errors, null, null);
        private static BuildResult Failure(IEnumerable<BuildError> errors, string timelinePath, string prefabPath)
        {
            var list = errors.Where(error => error != null).ToList(); foreach (var error in list) Debug.LogError("[UnityTimelineBuilder] " + error.Code + ": " + error.Message);
            return new BuildResult(false, timelinePath, prefabPath, null, list);
        }
        private static BuildError Unexpected(string sourcePath, Exception exception, int? lineNumber = null, string resourcePath = null)
        {
            Debug.LogException(exception); return new BuildError(BuildErrorCode.Unexpected, lineNumber, resourcePath ?? sourcePath, "Unexpected error: " + exception.Message);
        }
    }
}
