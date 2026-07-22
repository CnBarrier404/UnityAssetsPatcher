namespace UnityAssetsPatcher.Infrastructure.IO;

public interface IDirectoryOperations
{
    public void Create(string path);
    public void Move(string sourcePath, string destinationPath);
    public void Delete(string path);
}
