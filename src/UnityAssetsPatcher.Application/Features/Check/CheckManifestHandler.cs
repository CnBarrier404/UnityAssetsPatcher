using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Messaging;

namespace UnityAssetsPatcher.Application.Features.Check;

public sealed class CheckManifestHandler : IRequestHandler<CheckManifestRequest, CheckManifestResult>
{
    private readonly IModManifestService _manifestService;

    public CheckManifestHandler(IModManifestService manifestService)
    {
        ArgumentNullException.ThrowIfNull(manifestService);

        _manifestService = manifestService;
    }

    public async Task<CheckManifestResult> HandleAsync(
        CheckManifestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ModManifest manifest = await _manifestService
            .ReadManifestAsync(request.SourcePath, cancellationToken)
            .ConfigureAwait(false);

        return new CheckManifestResult(manifest);
    }
}
