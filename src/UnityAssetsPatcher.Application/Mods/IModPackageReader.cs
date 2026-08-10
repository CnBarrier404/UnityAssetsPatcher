namespace UnityAssetsPatcher.Application.Mods;

public interface IModPackageReader
{
    public IModPackageSession Open(string packagePath);
}
