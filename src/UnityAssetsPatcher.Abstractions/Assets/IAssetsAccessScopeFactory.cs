namespace UnityAssetsPatcher.Abstractions.Assets;

public interface IAssetsAccessScopeFactory
{
    public IAssetsAccessScope CreateScope();
}
