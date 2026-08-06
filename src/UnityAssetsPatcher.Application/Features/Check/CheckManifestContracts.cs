using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Features.Check;

public sealed record CheckManifestRequest(string SourcePath) : IRequest<OperationResult<CheckManifestResult>>;

public sealed record CheckManifestResult(ModManifest Manifest);
