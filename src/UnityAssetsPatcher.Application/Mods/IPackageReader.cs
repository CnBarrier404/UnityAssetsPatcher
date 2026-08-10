using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public interface IPackageReader
{
    public OperationResult<IPackageSession> Open(string packagePath);
}
