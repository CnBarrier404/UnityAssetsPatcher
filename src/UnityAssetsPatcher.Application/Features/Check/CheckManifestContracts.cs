using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Messaging;

namespace UnityAssetsPatcher.Application.Features.Check;

public sealed record CheckManifestRequest(string SourcePath) : IRequest<CheckManifestResult>;

public sealed record CheckManifestResult(ModManifest Manifest);
