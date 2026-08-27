using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Updates;

namespace UnityAssetsPatcher.Infrastructure.Updates;

internal sealed class GitHubUpdateChecker : IUpdateChecker
{
    private readonly GitHubUpdateManifestClient _manifestClient;
    private readonly string _currentVersion;
    private readonly ILogger<GitHubUpdateChecker> _logger;

    public GitHubUpdateChecker(
        GitHubUpdateManifestClient manifestClient,
        string currentVersion,
        ILogger<GitHubUpdateChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(manifestClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
        ArgumentNullException.ThrowIfNull(logger);

        _manifestClient = manifestClient;
        _currentVersion = currentVersion;
        _logger = logger;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        UpdateLog.UpdateCheckStarted(_logger);

        if (!SemanticVersion.TryParse(_currentVersion, out SemanticVersion currentVersion))
        {
            UpdateLog.UpdateCheckSkipped(_logger);

            return null;
        }

        UpdateInfo manifest = await _manifestClient
            .FetchAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!SemanticVersion.TryParse(manifest.Version, out SemanticVersion latestVersion))
        {
            UpdateLog.UpdateManifestRejected(_logger);

            throw new InvalidDataException("The update manifest contains an invalid version.");
        }

        if (latestVersion.CompareTo(currentVersion) <= 0)
        {
            UpdateLog.UpdateCheckCompletedWithoutUpdate(
                _logger,
                _currentVersion,
                manifest.Version);

            return null;
        }

        UpdateLog.UpdateCheckCompletedWithUpdate(
            _logger,
            _currentVersion,
            manifest.Version);

        return manifest;
    }
}
