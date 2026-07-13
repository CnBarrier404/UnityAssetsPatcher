namespace UnityAssetsPatcher.Application.Contracts;

public sealed record AvailableUpdate(string Version, Uri ReleaseUrl);

public interface IUpdateChecker
{
    public AvailableUpdate? CheckForUpdate();
}
