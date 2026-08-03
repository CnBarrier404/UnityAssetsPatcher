using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class CheckManifestWorkflow
{
    private readonly ManifestSourceReader _manifestSourceReader;
    private readonly ILogger<CheckManifestWorkflow> _logger;

    public CheckManifestWorkflow(ManifestSourceReader manifestSourceReader, ILogger<CheckManifestWorkflow> logger)
    {
        ArgumentNullException.ThrowIfNull(manifestSourceReader);
        ArgumentNullException.ThrowIfNull(logger);

        _manifestSourceReader = manifestSourceReader;
        _logger = logger;
    }

    public async Task<OperationResult<CheckManifestResult>> RunAsync(
        CheckManifestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operationId = Guid.NewGuid();

        using IDisposable? operationScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["OperationId"] = operationId,
            ["WorkflowType"] = nameof(CheckManifestWorkflow),
            ["ManifestPath"] = request.SourcePath,
        });

        var stopwatch = Stopwatch.StartNew();

        CheckManifestLog.OperationStarted(_logger, request.SourcePath);

        try
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            CheckManifestLog.OperationCanceled(_logger, stopwatch.Elapsed.TotalMilliseconds);

            throw;
        }
        catch (FileNotFoundException)
        {
            return Failure(FileErrorCodes.NotFound, request.SourcePath, stopwatch);
        }
        catch (DirectoryNotFoundException)
        {
            return Failure(FileErrorCodes.NotFound, request.SourcePath, stopwatch);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(FileErrorCodes.AccessDenied, request.SourcePath, stopwatch);
        }
        catch (InvalidDataException) when (IsPackagePath(request.SourcePath))
        {
            return Failure(ModPackageErrorCodes.InvalidArchive, request.SourcePath, stopwatch, "package_path");
        }
        catch (IOException)
        {
            return Failure(FileErrorCodes.ReadFailed, request.SourcePath, stopwatch);
        }
        catch (Exception exception)
        {
            CheckManifestLog.OperationFaulted(_logger, stopwatch.Elapsed.TotalMilliseconds, exception);

            throw;
        }
    }

    private static bool IsPackagePath(string? sourcePath)
    {
        return sourcePath is not null &&
               Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase);
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
