using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;

namespace UnityAssetsPatcher.Application.Features.Check;

public sealed class CheckManifestHandler : IRequestHandler<CheckManifestRequest, OperationResult<CheckManifestResult>>
{
    private readonly ManifestSourceReader _manifestSourceReader;
    private readonly ILogger<CheckManifestHandler> _logger;

    public CheckManifestHandler(ManifestSourceReader manifestSourceReader, ILogger<CheckManifestHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(manifestSourceReader);
        ArgumentNullException.ThrowIfNull(logger);

        _manifestSourceReader = manifestSourceReader;
        _logger = logger;
    }

    public async Task<OperationResult<CheckManifestResult>> HandleAsync(
        CheckManifestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operationId = Guid.NewGuid();

        using IDisposable? operationScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["OperationId"] = operationId,
            ["HandlerType"] = nameof(CheckManifestHandler),
            ["ManifestPath"] = request.SourcePath,
        });

        var stopwatch = Stopwatch.StartNew();

        CheckManifestLog.OperationStarted(_logger, request.SourcePath);

        try
        {
            return await ExecuteAsync(request, stopwatch, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            CheckManifestLog.OperationCanceled(_logger, stopwatch.Elapsed.TotalMilliseconds);

            throw;
        }
        catch (Exception exception)
        {
            var failure = CreateExpectedFailure(
                exception,
                request.SourcePath,
                stopwatch);

            if (failure is not null)
            {
                return failure;
            }

            CheckManifestLog.OperationFaulted(_logger, stopwatch.Elapsed.TotalMilliseconds, exception);

            throw;
        }
    }

    private async Task<OperationResult<CheckManifestResult>> ExecuteAsync(
        CheckManifestRequest request,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var readResult = await _manifestSourceReader
            .ReadAsync(request.SourcePath, cancellationToken)
            .ConfigureAwait(false);

        if (readResult is OperationFailed<byte[]> readFailure)
        {
            return Failure(readFailure.Error, stopwatch);
        }

        byte[] manifestBytes = ((OperationSucceeded<byte[]>)readResult).Value;
        var parseResult = ModManifestParser.Parse(manifestBytes);

        if (parseResult is OperationFailed<ModManifest> parseFailure)
        {
            return Failure(parseFailure.Error, stopwatch);
        }

        ModManifest manifest = ((OperationSucceeded<ModManifest>)parseResult).Value;
        var result = new CheckManifestResult(manifest);

        CheckManifestLog.OperationSucceeded(
            _logger,
            manifest.Name,
            manifest.Version,
            stopwatch.Elapsed.TotalMilliseconds);

        return new OperationSucceeded<CheckManifestResult>(result);
    }

    private OperationFailed<CheckManifestResult>? CreateExpectedFailure(
        Exception exception,
        string? sourcePath,
        Stopwatch stopwatch)
    {
        return exception switch
        {
            FileNotFoundException => Failure(FileErrorCodes.NotFound, sourcePath, stopwatch),
            DirectoryNotFoundException => Failure(FileErrorCodes.NotFound, sourcePath, stopwatch),
            UnauthorizedAccessException => Failure(FileErrorCodes.AccessDenied, sourcePath, stopwatch),
            InvalidDataException when IsPackagePath(sourcePath) =>
                Failure(ModPackageErrorCodes.InvalidArchive, sourcePath, stopwatch, "package_path"),
            IOException => Failure(FileErrorCodes.ReadFailed, sourcePath, stopwatch),
            _ => null,
        };
    }

    private static bool IsPackagePath(string? sourcePath)
    {
        return sourcePath is not null
               && Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private OperationFailed<CheckManifestResult> Failure(OperationError error, Stopwatch stopwatch)
    {
        CheckManifestLog.OperationFailed(_logger, error.Code.Value, stopwatch.Elapsed.TotalMilliseconds);

        return new OperationFailed<CheckManifestResult>(error);
    }

    private OperationFailed<CheckManifestResult> Failure(
        OperationErrorCode code,
        string? path,
        Stopwatch stopwatch,
        string pathParameter = "path")
    {
        var error = new OperationError(
            code,
            new Dictionary<string, object?>
            {
                [pathParameter] = path,
            });

        return Failure(error, stopwatch);
    }
}
