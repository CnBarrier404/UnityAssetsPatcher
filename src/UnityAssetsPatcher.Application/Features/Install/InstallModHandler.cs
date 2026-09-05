using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Features.Install;

public sealed class InstallModHandler :
    IRequestHandler<PreviewInstallRequest, OperationResult<InstallPreviewResult>>,
    IRequestHandler<InstallModRequest, OperationResult<InstallModResult>>
{
    private readonly InstallPlanBuilder _planBuilder;
    private readonly IRepository _repository;
    private readonly IAssetsAccessScopeFactory _assetsAccessScopeFactory;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ModPackageReader _modPackageReader;
    private readonly ILogger<InstallModHandler> _logger;

    public InstallModHandler(
        InstallPlanBuilder planBuilder,
        IRepository repository,
        IAssetsAccessScopeFactory assetsAccessScopeFactory,
        IFileSystemOperations fileSystemOperations,
        ModPackageReader modPackageReader,
        ILogger<InstallModHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(planBuilder);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(assetsAccessScopeFactory);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(modPackageReader);

        _planBuilder = planBuilder;
        _repository = repository;
        _assetsAccessScopeFactory = assetsAccessScopeFactory;
        _fileSystemOperations = fileSystemOperations;
        _modPackageReader = modPackageReader;
        _logger = logger ?? NullLogger<InstallModHandler>.Instance;
    }

    public async Task<OperationResult<InstallPreviewResult>> HandleAsync(
        PreviewInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Request);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await InvokeAsync(
            () => PreviewAsync(request.Request, cancellationToken),
            DirectoryError(request.Request.GameDirectory),
            nameof(PreviewAsync)).ConfigureAwait(false);

        return result;
    }

    public async Task<OperationResult<InstallModResult>> HandleAsync(
        InstallModRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Request);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await InvokeAsync(
            () => InstallAsync(request.Request, cancellationToken),
            DirectoryError(request.Request.GameDirectory),
            nameof(InstallAsync)).ConfigureAwait(false);

        return result;
    }

    private async Task<OperationResult<InstallPreviewResult>> PreviewAsync(
        InstallRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Previewing mod install from {ZipFilePath}", request.ZipFilePath);
        var timings = new StepTimer();
        var packageResult = await _modPackageReader.OpenAsync(
            request.ZipFilePath,
            request.SelectedOptionalGroups,
            timings,
            cancellationToken).ConfigureAwait(false);

        if (packageResult is OperationFailed<ModPackage> packageFailure)
        {
            return new OperationFailed<InstallPreviewResult>(packageFailure.Error);
        }

        using ModPackage package = ((OperationSucceeded<ModPackage>)packageResult).Value;
        using IAssetsAccessScope assetsScope = _assetsAccessScopeFactory.CreateScope();
        InstallAnalysisMode mode = request.IncludePatchPreviewDetails
            ? InstallAnalysisMode.PreviewDetailed
            : InstallAnalysisMode.PreviewSummary;
        InstallAnalysis analysis = _planBuilder.Analyze(
            package,
            request.GameDirectory,
            mode,
            assetsScope.Reader,
            timings);
        PreparedInstall preparedInstall = CreatePreparedInstall(request, analysis);

        InstallPreviewResult result = InstallResultMapper.ToPreviewResult(
                analysis,
                timings.BuildSnapshot()) with
            {
                PreparedInstall = preparedInstall
            };

        return new OperationSucceeded<InstallPreviewResult>(result);
    }

    private async Task<OperationResult<InstallModResult>> InstallAsync(
        InstallRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Installing mod from {ZipFilePath}", request.ZipFilePath);
        var timings = new StepTimer();

        var packageResult = await _modPackageReader.OpenAsync(
            request.ZipFilePath,
            request.SelectedOptionalGroups,
            timings,
            cancellationToken).ConfigureAwait(false);

        if (packageResult is OperationFailed<ModPackage> packageFailure)
        {
            return new OperationFailed<InstallModResult>(packageFailure.Error);
        }

        using ModPackage package = ((OperationSucceeded<ModPackage>)packageResult).Value;
        PreparedInstall? preparedInstall = request.PreparedInstall;
        InstallAnalysis analysis;

        using (IAssetsAccessScope assetsScope = _assetsAccessScopeFactory.CreateScope())
        {
            analysis = preparedInstall is null
                ? _planBuilder.Analyze(
                    package,
                    request.GameDirectory,
                    InstallAnalysisMode.Apply,
                    assetsScope.Reader,
                    timings)
                : PrepareAnalysis(request, package, preparedInstall, assetsScope.Reader, timings);
        }

        RepositoryInstallResult repositoryResult = await _repository.InstallModAsync(new InstallModPlan(
            request.ZipFilePath,
            analysis,
            preparedInstall?.AssetFiles), cancellationToken).ConfigureAwait(false);
        timings.Append(repositoryResult.Timing);
        InstallExecutionResult execution = repositoryResult.Execution;

        _logger.LogInformation(
            "Installed {ModName} {ModVersion}: {PatchedFileCount} files patched, " +
            "{CopiedFileCount} files copied, install id {InstallId}",
            analysis.Manifest.Name,
            analysis.Manifest.Version,
            execution.PatchedFiles.Count,
            execution.CopiedFiles.Count,
            execution.InstallId);

        InstallModResult result = InstallResultMapper.ToInstallResult(
                analysis,
                execution.PatchedFiles,
                execution.CopiedFiles,
                execution.InstallId,
                execution.BaseSnapshotCount,
                timings.BuildSnapshot()) with
            {
                Recovery = repositoryResult.Recovery
            };

        return new OperationSucceeded<InstallModResult>(result);
    }

    private async Task<OperationResult<TResult>> InvokeAsync<TResult>(
        Func<Task<OperationResult<TResult>>> operation,
        OperationErrorCode directoryError,
        string operationName)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (RepositoryRecoveryException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.RecoveryRequired,
                recovery: exception.Recovery);
            _logger.LogWarning(
                "Install operation {OperationName} requires backup recovery",
                operationName);

            return new OperationFailed<TResult>(error);
        }
        catch (PatchPlanningException exception)
        {
            var error = new OperationError(
                PatchErrorCodes.PlanningFailed,
                new Dictionary<string, object?>
                {
                    ["diagnosticCode"] = exception.Diagnostic.Code.ToString(),
                    ["path"] = exception.Diagnostic.AssetsFilePath
                });

            return new OperationFailed<TResult>(error);
        }
        catch (InstallPreparationStaleException)
        {
            return ExpectedFailure<TResult>(operationName, ModOperationErrorCodes.InstallPreviewStale, null);
        }
        catch (LayerPackageValidationException exception)
        {
            return new OperationFailed<TResult>(exception.Error);
        }
        catch (LayerPackageIntegrityException exception)
        {
            return ExpectedFailure<TResult>(
                operationName,
                ModOperationErrorCodes.FileIntegrityMismatch,
                null,
                exception.PackagePath);
        }
        catch (FileNotFoundException exception)
        {
            return ExpectedFailure<TResult>(
                operationName, FileErrorCodes.NotFound, exception.Message, exception.FileName);
        }
        catch (DirectoryNotFoundException exception)
        {
            return ExpectedFailure<TResult>(operationName, directoryError, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.AccessDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.SystemFailure, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return ExpectedFailure<TResult>(
                operationName,
                RepositoryErrorCodes.UnsupportedVersion,
                exception.Message);
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
            "Install operation {OperationName} failed with {ErrorCode}: {@Parameters}",
            operationName,
            code,
            parameters);

        return new OperationFailed<TResult>(error);
    }

    private PreparedInstall CreatePreparedInstall(
        InstallRequest request,
        InstallAnalysis analysis)
    {
        string zipFilePath = TrustedPath.NormalizeAbsolutePath(request.ZipFilePath);
        string? gameDirectory = NormalizeOptionalPath(request.GameDirectory);
        string[] assetFilePaths = analysis.Targets
            .Select(target => target.AssetsFilePath)
            .Distinct(TrustedPath.PathComparer)
            .ToArray();

        return new PreparedInstall(
            zipFilePath,
            gameDirectory,
            request.SelectedOptionalGroups.ToArray(),
            _fileSystemOperations.ComputeFileIntegrity(zipFilePath),
            [
                .. assetFilePaths.Select(path => new PreparedInstallAssetFile(
                    path,
                    _fileSystemOperations.ComputeFileIntegrity(path)))
            ]);
    }

    private InstallAnalysis PrepareAnalysis(
        InstallRequest request,
        ModPackage package,
        PreparedInstall preparedInstall,
        IAssetsFileReader assetsReader,
        StepTimer timings)
    {
        ValidatePreparedInstall(request, preparedInstall);

        return _planBuilder.Analyze(
            package,
            request.GameDirectory,
            InstallAnalysisMode.Apply,
            assetsReader,
            timings);
    }

    private void ValidatePreparedInstall(InstallRequest request, PreparedInstall preparedInstall)
    {
        if (!TrustedPath.PathsEqual(request.ZipFilePath, preparedInstall.ZipFilePath))
        {
            throw new InstallPreparationStaleException(
                "The install preview does not match the selected mod package.");
        }

        string? gameDirectory = NormalizeOptionalPath(request.GameDirectory);
        if (!PathsEqual(gameDirectory, preparedInstall.GameDirectory))
        {
            throw new InstallPreparationStaleException(
                "The install preview does not match the selected game directory.");
        }

        if (!OptionalGroupsMatch(request.SelectedOptionalGroups, preparedInstall.SelectedOptionalGroups))
        {
            throw new InstallPreparationStaleException(
                "The install preview does not match the selected optional groups.");
        }

        FileIntegrity actualPackageIntegrity = _fileSystemOperations.ComputeFileIntegrity(request.ZipFilePath);
        if (!preparedInstall.PackageIntegrity.Matches(actualPackageIntegrity))
        {
            throw new InstallPreparationStaleException(
                "The mod package changed after the install preview.");
        }
    }

    private static OperationErrorCode DirectoryError(string? gameDirectory)
    {
        return string.IsNullOrWhiteSpace(gameDirectory)
            ? GameDirectoryErrorCodes.Required
            : GameDirectoryErrorCodes.NotFound;
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return TrustedPath.PathsEqual(left, right);
    }

    private static bool OptionalGroupsMatch(
        IReadOnlyList<string> selectedOptionalGroups,
        IReadOnlyList<string> preparedOptionalGroups)
    {
        var selected = selectedOptionalGroups.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prepared = preparedOptionalGroups.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selected.SetEquals(prepared);
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : TrustedPath.NormalizeAbsolutePath(path);
    }
}
