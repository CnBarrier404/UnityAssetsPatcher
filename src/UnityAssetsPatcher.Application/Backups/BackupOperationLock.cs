namespace UnityAssetsPatcher.Application.Backups;

public sealed class BackupOperationLock : IDisposable
{
    private readonly FileStream _stream;

    private BackupOperationLock(FileStream stream)
    {
        _stream = stream;
    }

    public static BackupOperationLock Acquire(string backupDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
        Directory.CreateDirectory(backupDirectory);
        string path = Path.Combine(backupDirectory, ".operations.lock");

        try
        {
            return new BackupOperationLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.None));
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"Another install or uninstall operation is using the backup directory: {backupDirectory}", exception);
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
