namespace UnityAssetsPatcher.Abstractions.IO;

public interface IFileOperations
{
    public void Write(string destinationPath, Action<Stream> writer);
    public void Copy(string sourcePath, string destinationPath);
    public void Move(string sourcePath, string destinationPath);
    public void Delete(string path);
}
