namespace UnityAssetsPatcher.Application.Updates;

public sealed record UpdateInfo(string Version, Uri ReleaseUrl, Uri DownloadUrl, string Sha256);

public interface IUpdateChecker
{
    public Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
