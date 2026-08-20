using System;
using NUnit.Framework;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor.Tests
{
    public sealed class ResourceResolverRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            ResourceResolverRegistry.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            ResourceResolverRegistry.ResetForTest();
        }

        [Test]
        public void RegisterAndTryGetFindsResolverByKindIgnoringCaseAndWhitespace()
        {
            var resolver = new FakeResolver(" Audio ");

            ResourceResolverRegistry.Register(resolver);

            Assert.That(ResourceResolverRegistry.TryGet("  audio", out var found), Is.True);
            Assert.That(found, Is.SameAs(resolver));
        }

        [Test]
        public void RegisterReplacesResolverForExistingKind()
        {
            var original = new FakeResolver("Audio");
            var replacement = new FakeResolver(" audio ");
            ResourceResolverRegistry.Register(original);

            ResourceResolverRegistry.Register(replacement);

            Assert.That(ResourceResolverRegistry.TryGet("Audio", out var found), Is.True);
            Assert.That(found, Is.SameAs(replacement));
        }

        [Test]
        public void ResetForTestClearsRegisteredResolvers()
        {
            ResourceResolverRegistry.Register(new FakeResolver("Custom"));

            ResourceResolverRegistry.ResetForTest();

            Assert.That(ResourceResolverRegistry.TryGet("Custom", out _), Is.False);
        }

        [Test]
        public void RegisterRejectsNullAndEmptyKinds()
        {
            Assert.Throws<ArgumentNullException>(() => ResourceResolverRegistry.Register(null));
            Assert.Throws<ArgumentException>(() => ResourceResolverRegistry.Register(new FakeResolver(" ")));
        }

        [Test]
        public void TryGetReturnsFalseForMissingOrEmptyKinds()
        {
            Assert.That(ResourceResolverRegistry.TryGet(null, out _), Is.False);
            Assert.That(ResourceResolverRegistry.TryGet(" ", out _), Is.False);
            Assert.That(ResourceResolverRegistry.TryGet("Missing", out _), Is.False);
        }

        private sealed class FakeResolver : IResourceResolver
        {
            public string ResourceKind { get; }
            public Type AssetType => typeof(TextAsset);

            public FakeResolver(string resourceKind)
            {
                ResourceKind = resourceKind;
            }

            public bool TryResolve(ClipRow row, ResolveContext context,
                out UnityEngine.Object asset, out BuildError error)
            {
                asset = null;
                error = null;
                return false;
            }
        }
    }
}
