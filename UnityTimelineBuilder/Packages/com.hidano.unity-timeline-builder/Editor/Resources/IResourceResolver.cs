using System;
using UnityEngine;

namespace Hidano.UnityTimelineBuilder.Editor
{
    internal interface IResourceResolver
    {
        string ResourceKind { get; }
        Type AssetType { get; }

        bool TryResolve(ClipRow row, ResolveContext context,
            out UnityEngine.Object asset, out BuildError error);
    }
}
