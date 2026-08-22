using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
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
                ? Path.GetFileNameWithoutExtension(request.SheetPath)
                : BuildSheetParser.NormalizeAssetName(request.AssetName);
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

            var completedOutputs = new List<BuildOutput>();
            foreach (var plan in plans)
                foreach (var warning in plan.Warnings)
                    Debug.LogWarning("[UnityTimelineBuilder] " + warning);

            var sceneSaveConfirmed = Application.isBatchMode;
            for (var groupIndex = 0; groupIndex < plans.Count; groupIndex++)
            {
                var plan = plans[groupIndex];
                var group = parsed.Groups[groupIndex];
                var output = new BuildOutput(group.TimelineName, plan.AssetName,
                    null, null, null, group.ScenePlan != null);
                try
                {
                    EnsureOutputDirectory(request.OutputDirectory);
                    var timeline = new TimelineAssetFactory().Create(resolvedByGroup[groupIndex], plan.TimelineAssetPath);
                    output = new BuildOutput(group.TimelineName, plan.AssetName,
                        plan.TimelineAssetPath, null, null, group.ScenePlan != null);
                    new PrefabFactory().Create(timeline, plan.PrefabPath, plan.AssetName);
                    output = new BuildOutput(group.TimelineName, plan.AssetName,
                        plan.TimelineAssetPath, plan.PrefabPath, null, group.ScenePlan != null);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(plan.TimelineAssetPath, ImportAssetOptions.ForceSynchronousImport);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    var persistedTimeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(plan.TimelineAssetPath);

                    if (group.ScenePlan != null)
                    {
                        if (!sceneSaveConfirmed)
                        {
                            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            {
                                var canceled = new BuildError(BuildErrorCode.SceneBuildCanceled,
                                    group.ScenePlan.Definition.LineNumber, plan.ScenePath,
                                    "Scene build was canceled because modified scenes were not saved.",
                                    group.TimelineName);
                                return new BuildResult(false, completedOutputs.Concat(new[] { output }).ToArray(),
                                    new[] { canceled });
                            }
                            sceneSaveConfirmed = true;
                        }

                        var sceneValidation = sceneValidationByGroup[groupIndex];
                        var sceneTimeline = string.IsNullOrWhiteSpace(group.ScenePlan.Definition.TimelineAssetPath)
                            ? persistedTimeline : sceneValidation.Timeline;
                        if (sceneTimeline == null)
                        {
                            var missing = new BuildError(BuildErrorCode.SceneTimelineNotFound,
                                group.ScenePlan.Definition.LineNumber, plan.TimelineAssetPath,
                                "Generated TimelineAsset was not found after creation.", group.TimelineName);
                            return new BuildResult(false, completedOutputs.Concat(new[] { output }).ToArray(),
                                new[] { missing });
                        }

                        var sceneContext = new SceneBuildContext(group.ScenePlan, sceneTimeline,
                            sceneValidation.PrefabAssets, plan.ScenePath, plan.AssetName,
                            string.IsNullOrWhiteSpace(group.ScenePlan.Definition.TimelineAssetPath)
                                ? plan.TimelineAssetPath : group.ScenePlan.Definition.TimelineAssetPath,
                            plan.PrefabPath);
                        if (!new SceneFactory().TryCreate(sceneContext, out var createdScenePath, out var sceneErrors))
                        {
                            var attributed = sceneErrors.Select(error => WithTimeline(error, group.TimelineName));
                            return new BuildResult(false, completedOutputs.Concat(new[] { output }).ToArray(),
                                attributed.ToArray());
                        }
                        output = new BuildOutput(group.TimelineName, plan.AssetName,
                            plan.TimelineAssetPath, plan.PrefabPath, createdScenePath, true);
                    }

                    completedOutputs.Add(output);
                }
                catch (Exception exception)
                {
                    var failure = new BuildError(BuildErrorCode.OutputWriteFailed, null,
                        plan.TimelineAssetPath, exception.Message, group.TimelineName);
                    return new BuildResult(false, completedOutputs.Concat(new[] { output }).ToArray(),
                        new[] { failure });
                }
            }

            return new BuildResult(true, completedOutputs, Array.Empty<BuildError>());
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

        private static BuildError WithTimeline(BuildError error, string timelineName)
        {
            if (error == null) return null;
            return new BuildError(error.Code, error.LineNumber, error.SourcePath,
                error.Message, error.TimelineName ?? timelineName);
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
