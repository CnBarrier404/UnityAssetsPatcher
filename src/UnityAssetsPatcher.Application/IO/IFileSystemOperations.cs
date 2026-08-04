using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.IO;

public enum FileDestinationMode
{
    CreateNew,
    ReplaceExisting,
    CreateOrReplace
}

public interface IFileSystemOperations
{
    public Stream OpenRead(string path);

    public FileIntegrity ComputeFileIntegrity(string path);

    public FileAttributes GetAttributes(string path);

    public void WriteFileAtomically(string destinationPath, FileDestinationMode mode, Action<Stream> writer);

    public void CopyFileAtomically(string sourcePath, string destinationPath, FileDestinationMode mode);

    public void DeleteFile(string path);

    public void EnsureDirectory(string path);

    public void MoveDirectory(string sourcePath, string destinationPath);

    public void DeleteDirectoryTree(string path);
}
