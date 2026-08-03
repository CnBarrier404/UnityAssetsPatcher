namespace UnityAssetsPatcher.Application.Installation;

public interface IGameInstallationLocator
{
    public IReadOnlyList<string> FindGameDirectories(string game);
}
