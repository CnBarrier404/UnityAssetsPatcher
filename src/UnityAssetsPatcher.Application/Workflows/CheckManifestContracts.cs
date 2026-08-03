using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed record CheckManifestRequest(string SourcePath);

public sealed record CheckManifestResult(ModManifest Manifest);
