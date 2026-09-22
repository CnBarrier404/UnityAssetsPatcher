namespace UnityAssetsPatcher.Application.Updates;

public sealed record UpdateInfo(string Version, Uri ReleaseUrl);

public interface IUpdateChecker
{
    public Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
