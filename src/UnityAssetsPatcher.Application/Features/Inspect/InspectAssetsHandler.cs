using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Features.Inspect;

public sealed class InspectAssetsHandler :
    IRequestHandler<InspectListRequest, OperationResult<InspectListResult>>,
    IRequestHandler<InspectFieldsRequest, OperationResult<AssetField>>
{
    private readonly IAssetsFileReader _assetsReader;
    private readonly ILogger<InspectAssetsHandler> _logger;

    public InspectAssetsHandler(
        IAssetsFileReader assetsReader,
        ILogger<InspectAssetsHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(assetsReader);

        _assetsReader = assetsReader;
        _logger = logger ?? NullLogger<InspectAssetsHandler>.Instance;
    }

    public Task<OperationResult<InspectListResult>> HandleAsync(
        InspectListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        OperationResult<InspectListResult> result = Invoke(
            () => List(request),
            nameof(List));

        return Task.FromResult(result);
    }

    public Task<OperationResult<AssetField>> HandleAsync(
        InspectFieldsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        OperationResult<AssetField> result = Invoke(
            () => Fields(request),
            nameof(Fields));

        return Task.FromResult(result);
    }

    private InspectListResult List(InspectListRequest request)
    {
        IReadOnlyList<AssetInfo> assets = _assetsReader.ReadAssets(request.AssetsFilePath);
        IEnumerable<AssetInfo> listedAssets = request.Limit is null
            ? assets
            : assets.Take(request.Limit.Value);
        InspectAssetSummary[] summaries = listedAssets
            .Select(asset => new InspectAssetSummary(
                asset.PathId,
                asset.TypeName,
                ReadName(request.AssetsFilePath, asset.PathId)))
            .ToArray();

        _logger.LogInformation(
            "Inspected {AssetsFilePath}: {ListedAssetCount} of {TotalAssetCount} assets listed",
            request.AssetsFilePath,
            summaries.Length,
            assets.Count);

        return new InspectListResult(summaries, assets.Count);
    }

    private AssetField Fields(InspectFieldsRequest request)
    {
        return _assetsReader.ReadField(request.AssetsFilePath, request.PathId);
    }

    private string? ReadName(string assetsFilePath, long pathId)
    {
        try
        {
            AssetField fieldTree = _assetsReader.ReadField(assetsFilePath, pathId);

            return fieldTree.FindChild("m_Name")?.Value?.ToInvariantString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private OperationResult<TResult> Invoke<TResult>(Func<TResult> operation, string operationName)
    {
        try
        {
            TResult result = operation();

            return new OperationSucceeded<TResult>(result);
        }
        catch (FileNotFoundException exception)
        {
            return ExpectedFailure<TResult>(
                operationName, FileErrorCodes.NotFound, exception.Message, exception.FileName);
        }
        catch (DirectoryNotFoundException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.DirectoryNotFound, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.AccessDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.SystemFailure, exception.Message);
        }
        catch (JsonException exception)
        {
            return ExpectedFailure<TResult>(operationName, ModPackageErrorCodes.InvalidPackage, exception.Message);
        }
        catch (InvalidDataException exception)
        {
            return ExpectedFailure<TResult>(operationName, ModPackageErrorCodes.InvalidPackage, exception.Message);
        }
        catch (InvalidOperationException exception) when (operationName == nameof(Fields))
        {
            return ExpectedFailure<TResult>(operationName, AssetErrorCodes.NotFound, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Inspect operation {OperationName} failed", operationName);

            throw;
        }
    }

    private OperationFailed<TResult> ExpectedFailure<TResult>(
        string operationName,
        OperationErrorCode code,
        string? detail,
        string? path = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(detail))
        {
            parameters["detail"] = detail;
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            parameters["path"] = path;
        }

        var error = new OperationError(code, parameters);
        _logger.LogWarning(
            "Inspect operation {OperationName} failed with {ErrorCode}: {@Parameters}",
            operationName,
            code,
            parameters);

        return new OperationFailed<TResult>(error);
    }
}
