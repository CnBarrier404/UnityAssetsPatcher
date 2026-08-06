using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal static class FileCompositionStoreSupport
{
    public static string NormalizeIdentifier(string value, string parameterName)
    {
        if (!TrustedPath.TryNormalizeRelativePath(value, out string normalizedPath) ||
            normalizedPath.IndexOfAny(['\\', '/', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException($"The identifier must be a single path segment: '{value}'.", parameterName);
        }

        return normalizedPath;
    }

    public static void EnsureRegularFile(IFileSystemOperations fileSystemOperations, string path, string description)
    {
        FileAttributes attributes = fileSystemOperations.GetAttributes(path);

        if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException($"{description} must be a regular file: {path}");
        }
    }

    public static void EnsureRealDirectory(
        IFileSystemOperations fileSystemOperations,
        string path,
        string description)
    {
        FileAttributes attributes = fileSystemOperations.GetAttributes(path);

        if (!attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException($"{description} must be a real directory: {path}");
        }
    }

    public static bool TryGetAttributes(
        IFileSystemOperations fileSystemOperations,
        string path,
        out FileAttributes attributes)
    {
        try
        {
            attributes = fileSystemOperations.GetAttributes(path);

            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            attributes = default;

            return false;
        }
    }

    public static string ResolvePreparedTransactionChild(
        IFileSystemOperations fileSystemOperations,
        TrustedPathResolver pathResolver,
        string transactionDirectory,
        string path,
        string description)
    {
        EnsureRealDirectory(fileSystemOperations, transactionDirectory, "Transaction directory");

        string fullPath = TrustedPath.NormalizeAbsolutePath(path);

        if (TrustedPath.PathsEqual(fullPath, transactionDirectory) ||
            !TrustedPath.IsWithinRoot(fullPath, transactionDirectory))
        {
            throw new InvalidOperationException($"{description} directory is outside the active transaction.");
        }

        string relativePath = Path.GetRelativePath(transactionDirectory, fullPath);

        return pathResolver.ResolveWithinDirectory(transactionDirectory, relativePath);
    }

    public static string ResolveExistingTransactionChild(
        IFileSystemOperations fileSystemOperations,
        TrustedPathResolver pathResolver,
        string transactionDirectory,
        string path,
        string description)
    {
        string resolvedPath = ResolvePreparedTransactionChild(
            fileSystemOperations,
            pathResolver,
            transactionDirectory,
            path,
            description);
        EnsureRealDirectory(fileSystemOperations, resolvedPath, $"{description} directory");

        return resolvedPath;
    }

    public static T ReadJson<T>(
        IFileSystemOperations fileSystemOperations,
        string path,
        JsonTypeInfo<T> typeInfo,
        string description)
    {
        try
        {
            using Stream stream = fileSystemOperations.OpenRead(path);

            return JsonSerializer.Deserialize(stream, typeInfo) ??
                   throw new InvalidDataException($"{description} could not be read: {path}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{description} contains invalid JSON: {path}", exception);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
                                              or NotSupportedException or OverflowException)
        {
            throw new InvalidDataException($"{description} contains invalid data: {path}", exception);
        }
    }

    public static void WriteJson<T>(
        IFileSystemOperations fileSystemOperations,
        string path,
        T value,
        JsonTypeInfo<T> typeInfo,
        FileDestinationMode mode)
    {
        fileSystemOperations.WriteFileAtomically(
            path,
            mode,
            stream => JsonSerializer.Serialize(stream, value, typeInfo));
    }
}
