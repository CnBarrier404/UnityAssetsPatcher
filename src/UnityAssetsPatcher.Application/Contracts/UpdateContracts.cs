namespace UnityAssetsPatcher.Application.Contracts;

public sealed record AvailableUpdate(string Version, Uri ReleaseUrl, Uri DownloadUrl, string Sha256);

public abstract record UpdateCheckResult;

public sealed record UpdateAvailable(AvailableUpdate Update) : UpdateCheckResult;

public sealed record UpToDate : UpdateCheckResult;

public sealed record UpdateCheckFailed : UpdateCheckResult;

public interface IUpdateChecker
{
    public Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
