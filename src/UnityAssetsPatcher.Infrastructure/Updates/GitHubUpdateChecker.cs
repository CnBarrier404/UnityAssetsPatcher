using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Updates;

namespace UnityAssetsPatcher.Infrastructure.Updates;

internal sealed class GitHubUpdateChecker : IUpdateChecker
{
    private readonly GitHubUpdateManifestClient _manifestClient;
    private readonly ILogger<GitHubUpdateChecker> _logger;
    private readonly AppInfo _appInfo;

    public GitHubUpdateChecker(
        GitHubUpdateManifestClient manifestClient,
        AppInfo appInfo,
        ILogger<GitHubUpdateChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(manifestClient);
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(logger);

        _manifestClient = manifestClient;
        _appInfo = appInfo;
        _logger = logger;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!SemanticVersion.TryParse(_appInfo.DisplayVersion, out SemanticVersion currentVersion))
        {
            UpdateLog.UpdateCheckSkipped(_logger, _appInfo.DisplayVersion);

            return new UpdateCheckFailed();
        }

        UpdateManifest? manifest = await _manifestClient.FetchAsync(cancellationToken).ConfigureAwait(false);

        if (manifest is null)
        {
            return new UpdateCheckFailed();
        }

        if (manifest.SemanticVersion.CompareTo(currentVersion) <= 0)
        {
            UpdateLog.NoUpdateAvailable(_logger, _appInfo.DisplayVersion, manifest.Version);

            return new UpToDate();
        }

        UpdateLog.UpdateAvailable(_logger, _appInfo.DisplayVersion, manifest.Version);

        return new UpdateAvailable(new AvailableUpdate(
            manifest.Version,
            manifest.ReleaseUrl,
            manifest.DownloadUrl,
            manifest.Sha256));
    }
}
