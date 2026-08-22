using NUnit.Framework;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class OutputPathPlannerTests
    {
        private const string OutputDirectory = "Assets/Generated";

        [Test]
        public void AssetNameCollisionIsCaseInsensitiveAndAddsSuffixWarning()
        {
            var outputs = Plan(
                Group("Main"),
                Group("main"));

            Assert.That(outputs[0].AssetName, Is.EqualTo("Main"));
            Assert.That(outputs[0].TimelineAssetPath, Is.EqualTo("Assets/Generated/Main.playable"));
            Assert.That(outputs[1].AssetName, Is.EqualTo("main (1)"));
            Assert.That(outputs[1].TimelineAssetPath, Is.EqualTo("Assets/Generated/main (1).playable"));
            Assert.That(outputs[1].PrefabPath, Is.EqualTo("Assets/Generated/main (1).prefab"));
            Assert.That(outputs[1].Warnings, Has.Count.EqualTo(1));
            StringAssert.Contains("'main'", outputs[1].Warnings[0]);
            StringAssert.Contains("Assets/Generated/main (1).playable", outputs[1].Warnings[0]);
        }

        [Test]
        public void SceneNameCollisionIsCaseInsensitiveAndAddsSuffixWarning()
        {
            var outputs = Plan(
                Group("First", Scene("Shot")),
                Group("Second", Scene("shot")));

            Assert.That(outputs[0].ScenePath, Is.EqualTo("Assets/Generated/Shot.unity"));
            Assert.That(outputs[1].ScenePath, Is.EqualTo("Assets/Generated/shot (1).unity"));
            Assert.That(outputs[1].Warnings, Has.Count.EqualTo(1));
            StringAssert.Contains("Scene", outputs[1].Warnings[0]);
            StringAssert.Contains("Assets/Generated/shot (1).unity", outputs[1].Warnings[0]);
        }

        [Test]
        public void SuffixCollisionContinuesUntilAnUnusedNameIsFound()
        {
            var outputs = Plan(
                Group("A"),
                Group("A (1)"),
                Group("a"));

            Assert.That(outputs[0].AssetName, Is.EqualTo("A"));
            Assert.That(outputs[1].AssetName, Is.EqualTo("A (1)"));
            Assert.That(outputs[2].AssetName, Is.EqualTo("a (2)"));
            Assert.That(outputs[2].Warnings, Has.Count.EqualTo(1));
        }

        [Test]
        public void AssetPairKeepsTheSameResolvedBaseNameAndFirstGroupKeepsItsName()
        {
            var outputs = Plan(
                Group("  Main  "),
                Group("main"));

            Assert.That(outputs[0].AssetName, Is.EqualTo("Main"));
            Assert.That(outputs[0].TimelineAssetPath, Does.EndWith("Main.playable"));
            Assert.That(outputs[0].PrefabPath, Does.EndWith("Main.prefab"));
            Assert.That(outputs[1].TimelineAssetPath, Does.EndWith("main (1).playable"));
            Assert.That(outputs[1].PrefabPath, Does.EndWith("main (1).prefab"));
            Assert.That(outputs[1].TimelineAssetPath.Replace(".playable", ""),
                Is.EqualTo(outputs[1].PrefabPath.Replace(".prefab", "")));
        }

        [Test]
        public void LegacyGroupUsesFallbackAssetNameAndSceneNameCanMatchTimelineName()
        {
            var outputs = PlanWithFallback("LegacyAsset", Group(null, Scene("LegacyAsset")));

            Assert.That(outputs[0].AssetName, Is.EqualTo("LegacyAsset"));
            Assert.That(outputs[0].TimelineAssetPath, Is.EqualTo("Assets/Generated/LegacyAsset.playable"));
            Assert.That(outputs[0].PrefabPath, Is.EqualTo("Assets/Generated/LegacyAsset.prefab"));
            Assert.That(outputs[0].ScenePath, Is.EqualTo("Assets/Generated/LegacyAsset.unity"));
            Assert.That(outputs[0].Warnings, Is.Empty);
        }

        private static System.Collections.Generic.IReadOnlyList<PlannedGroupOutputs> Plan(
            params TimelineGroupPlan[] groups)
        {
            return PlanWithFallback("Fallback", groups);
        }

        private static System.Collections.Generic.IReadOnlyList<PlannedGroupOutputs> PlanWithFallback(
            string fallbackAssetName, params TimelineGroupPlan[] groups)
        {
            return new OutputPathPlanner().Plan(groups, OutputDirectory, fallbackAssetName);
        }

        private static TimelineGroupPlan Group(string timelineName, SceneBuildPlan scenePlan = null)
        {
            return new TimelineGroupPlan(timelineName, 1, new ClipRow[0], scenePlan);
        }

        private static SceneBuildPlan Scene(string sceneName)
        {
            return new SceneBuildPlan(new SceneDefinitionRow(2, sceneName, null),
                new ScenePrefabRow[0], new SceneBindRow[0]);
        }
    }
}
