using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Features.Check;

public sealed class CheckManifestHandler : IRequestHandler<CheckManifestRequest, OperationResult<CheckManifestResult>>
{
    private readonly ModManifestReader _manifestReader;

    public CheckManifestHandler(ModManifestReader manifestReader)
    {
        ArgumentNullException.ThrowIfNull(manifestReader);

        _manifestReader = manifestReader;
    }

    public async Task<OperationResult<CheckManifestResult>> HandleAsync(
        CheckManifestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _manifestReader
            .ReadAsync(request.SourcePath, cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            OperationSucceeded<ModManifest> succeeded =>
                new OperationSucceeded<CheckManifestResult>(new CheckManifestResult(succeeded.Value)),

            OperationFailed<ModManifest> failed => new OperationFailed<CheckManifestResult>(failed.Error),

            _ => throw new InvalidOperationException("The manifest parser returned an unknown result.")
        };
    }
}
