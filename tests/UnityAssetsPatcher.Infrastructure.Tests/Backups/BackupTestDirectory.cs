namespace UnityAssetsPatcher.Infrastructure.Tests.Backups;

internal sealed class BackupTestDirectory : IDisposable
{
    public string Path { get; }

    public BackupTestDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"UnityAssetsPatcher-Backup-{Guid.NewGuid():N}");

        Directory.CreateDirectory(Path);
    }

    public string CreateDirectory(params string[] segments)
    {
        string path = GetPath(segments);

        Directory.CreateDirectory(path);

        return path;
    }

    public string GetPath(params string[] segments)
    {
        return segments.Aggregate(Path, System.IO.Path.Combine);
    }

    public string WriteFile(string relativePath, string contents)
    {
        string path = GetPath(relativePath);
        string? directory = System.IO.Path.GetDirectoryName(path);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);

        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
