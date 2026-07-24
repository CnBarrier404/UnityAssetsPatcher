namespace UnityAssetsPatcher.Abstractions.IO;

public interface IFileSystemOperations
{
    public string ResolveExistingDirectory(string path);
    public string ResolveWithinDirectory(string rootDirectory, string relativePath);
    public void WriteFile(string destinationPath, Action<Stream> writer);
    public void CopyFile(string sourcePath, string destinationPath);

    public void MoveFile(string sourcePath, string destinationPath);
    public void DeleteFile(string path);
    public void CreateDirectory(string path);
    public void MoveDirectory(string sourcePath, string destinationPath);
    public void DeleteDirectory(string path);
}
