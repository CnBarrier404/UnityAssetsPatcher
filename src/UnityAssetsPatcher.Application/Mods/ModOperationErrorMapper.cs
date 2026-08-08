using System.Text.Json;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

internal static class ModOperationErrorMapper
{
    public static bool TryMapManifestReadException(
        Exception exception,
        string sourcePath,
        out OperationError error)
    {
        ArgumentNullException.ThrowIfNull(exception);

        OperationError? mappedError = exception switch
        {
            FileNotFoundException or DirectoryNotFoundException =>
                CreateError(FileErrorCodes.NotFound, "path", sourcePath),
            UnauthorizedAccessException => CreateError(FileErrorCodes.AccessDenied, "path", sourcePath),
            InvalidDataException when (IsPackagePath(sourcePath)) =>
                CreateError(ModPackageErrorCodes.InvalidArchive, "package_path", sourcePath),
            JsonException jsonException => InvalidJson(jsonException),
            IOException => CreateError(FileErrorCodes.ReadFailed, "path", sourcePath),
            ArgumentException or NotSupportedException =>
                CreateError(FileErrorCodes.InvalidPath, "path", sourcePath),
            _ => null,
        };

        error = mappedError!;

        return mappedError is not null;
    }

    public static bool IsInvalidPackageException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is JsonException or InvalidDataException or NotSupportedException or InvalidOperationException;
    }

    public static string FormatPackageFailure(OperationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error.Code == ModPackageErrorCodes.ExtractionLimitExceeded)
        {
            string entryPath = GetParameter(error, "entry_path");
            string limit = GetParameter(error, "limit_bytes");

            return $"Zip package exceeds the maximum allowed total uncompressed size while extracting {entryPath}: " +
                   $"more than {limit} bytes.";
        }

        string parameters = error.Parameters.Count == 0
            ? string.Empty
            : $" ({string.Join(", ", error.Parameters.Select(parameter =>
                $"{parameter.Key}={parameter.Value}"))})";

        return $"Operation '{error.Code.Value}' failed{parameters}.";
    }

    private static OperationError InvalidJson(JsonException exception)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (exception.BytePositionInLine is { } bytePosition)
        {
            parameters.Add("byte_position", bytePosition);
        }

        if (exception.LineNumber is { } lineNumber)
        {
            parameters.Add("line_number", lineNumber);
        }

        return new OperationError(ManifestErrorCodes.InvalidJson, parameters);
    }

    private static OperationError CreateError(OperationErrorCode code, string parameterName, string? parameterValue)
    {
        return new OperationError(
            code,
            new Dictionary<string, object?>
            {
                [parameterName] = parameterValue,
            });
    }

    private static string GetParameter(OperationError error, string name)
    {
        return error.Parameters.TryGetValue(name, out object? value)
            ? value?.ToString() ?? "<unknown>"
            : "<unknown>";
    }

    private static bool IsPackagePath(string? sourcePath)
    {
        return sourcePath is not null &&
               Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }
}
