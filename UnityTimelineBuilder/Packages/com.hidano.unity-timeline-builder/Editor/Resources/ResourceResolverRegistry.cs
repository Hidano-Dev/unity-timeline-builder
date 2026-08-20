using System;
using System.Collections.Generic;

namespace Hidano.UnityTimelineBuilder.Editor
{
    internal static class ResourceResolverRegistry
    {
        private static readonly Dictionary<string, IResourceResolver> Resolvers =
            new Dictionary<string, IResourceResolver>(StringComparer.OrdinalIgnoreCase);

        public static void Register(IResourceResolver resolver)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            if (string.IsNullOrWhiteSpace(resolver.ResourceKind))
                throw new ArgumentException("Resource resolver kind is required.", nameof(resolver));

            Resolvers[resolver.ResourceKind.Trim()] = resolver;
        }

        public static bool TryGet(string resourceKind, out IResourceResolver resolver)
        {
            resolver = null;
            return !string.IsNullOrWhiteSpace(resourceKind)
                && Resolvers.TryGetValue(resourceKind.Trim(), out resolver);
        }

        internal static void ResetForTest()
        {
            Resolvers.Clear();
        }
    }
}
