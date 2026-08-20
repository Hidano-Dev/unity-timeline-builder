using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class TrackBuilderRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            TrackBuilderRegistry.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            TrackBuilderRegistry.ResetForTest();
        }

        [Test]
        public void BuiltInTrackTypesAreKnownCaseInsensitively()
        {
            Assert.That(TrackBuilderRegistry.IsKnownTrackType("audio"), Is.True);
            Assert.That(TrackBuilderRegistry.IsKnownTrackType(" ANIMATION "), Is.True);
        }

        [Test]
        public void RegisterAndTryGetFindsBuilderByTrimmedCaseInsensitiveKey()
        {
            var builder = new FakeBuilder(" Custom ", "Test");

            TrackBuilderRegistry.Register(builder);

            Assert.That(TrackBuilderRegistry.TryGet("custom", out var found), Is.True);
            Assert.That(found, Is.SameAs(builder));
            Assert.That(TrackBuilderRegistry.IsKnownTrackType(" CUSTOM "), Is.True);
        }

        [Test]
        public void RegisterReplacesBuilderForExistingKey()
        {
            var original = new FakeBuilder("Custom", "Test");
            var replacement = new FakeBuilder(" custom ", "Other");
            TrackBuilderRegistry.Register(original);

            TrackBuilderRegistry.Register(replacement);

            Assert.That(TrackBuilderRegistry.TryGet("CUSTOM", out var found), Is.True);
            Assert.That(found, Is.SameAs(replacement));
        }

        [Test]
        public void ResetForTestRestoresBuiltInsAndRemovesCustomBuilders()
        {
            TrackBuilderRegistry.Register(new FakeBuilder("Custom", "Test"));

            TrackBuilderRegistry.ResetForTest();

            Assert.That(TrackBuilderRegistry.IsKnownTrackType("Custom"), Is.False);
            Assert.That(TrackBuilderRegistry.IsKnownTrackType("Audio"), Is.True);
        }

        [Test]
        public void RegisterRejectsNullAndEmptyKeys()
        {
            Assert.Throws<ArgumentNullException>(() => TrackBuilderRegistry.Register(null));
            Assert.Throws<ArgumentException>(() => TrackBuilderRegistry.Register(new FakeBuilder(" ", "Test")));
        }

        [Test]
        public void TryGetReturnsFalseForMissingOrEmptyKeys()
        {
            Assert.That(TrackBuilderRegistry.TryGet(null, out _), Is.False);
            Assert.That(TrackBuilderRegistry.TryGet(" ", out _), Is.False);
            Assert.That(TrackBuilderRegistry.TryGet("Missing", out _), Is.False);
        }

        private sealed class FakeBuilder : ITrackBuilder
        {
            public string TrackTypeKey { get; }
            public string ResourceKind { get; }

            public FakeBuilder(string trackTypeKey, string resourceKind)
            {
                TrackTypeKey = trackTypeKey;
                ResourceKind = resourceKind;
            }

            public TrackAsset CreateTrack(TimelineAsset timeline, string trackName) => null;

            public void AddClip(TrackAsset track, ClipRow row, UnityEngine.Object resolvedAsset)
            {
            }
        }
    }
}
