namespace UnityAssetsPatcher.Application.IO;

public static class FileHelper
{
    public static void SafeMoveFile(string source, string destination, bool overwrite)
    {
        if (!overwrite || !File.Exists(destination))
        {
            File.Move(source, destination, overwrite);
            return;
        }

        FileAttributes attrs = File.GetAttributes(destination);

        if (!attrs.HasFlag(FileAttributes.ReparsePoint))
        {
            File.Move(source, destination, overwrite);
            return;
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Could not find file '{source}'.", source);
        }

        string backupPath = CreateTemporarySiblingPath(destination);
        File.Move(destination, backupPath);

        try
        {
            File.Move(source, destination);
            File.Delete(backupPath);
        }
        catch
        {
            if (!File.Exists(destination) && File.Exists(backupPath))
            {
                File.Move(backupPath, destination);
            }

            throw;
        }
    }

    private static string CreateTemporarySiblingPath(string path)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ??
                           throw new InvalidOperationException($"Cannot resolve file directory: {path}");
        string fileName = Path.GetFileName(path);

        string candidate;
        do
        {
            candidate = Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
        } while (File.Exists(candidate));

        return candidate;
    }
}
