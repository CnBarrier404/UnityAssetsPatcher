using System.Collections;
using System.Globalization;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;

namespace UnityAssetsPatcher.CLI;

internal static class CLITextOutput
{
    public static void WriteFailure(TextWriter error, OperationError operationError)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(operationError);

        error.WriteLine($"Error [{operationError.Code.Value}]: {Message(operationError)}");

        foreach ((string key, object? value) in VisibleParameters(operationError).OrderBy(parameter => parameter.Key))
        {
            error.WriteLine($"  {key}: {FormatValue(value)}");
        }
    }

    public static void WriteUnexpectedFailure(TextWriter error, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(exception);

        error.WriteLine($"Error [application.unexpected_failure]: {exception.Message}");
    }

    private static string Message(OperationError error)
    {
        OperationErrorCode code = error.Code;

        if (code == FileErrorCodes.NotFound)
        {
            return "File not found.";
        }

        if (code == FileErrorCodes.InvalidPath)
        {
            return "The file path is invalid.";
        }

        if (code == FileErrorCodes.AccessDenied)
        {
            return "Access to the file was denied.";
        }

        if (code == FileErrorCodes.ReadFailed)
        {
            return "The file could not be read.";
        }

        if (code == ModPackageErrorCodes.InvalidArchive)
        {
            return "The mod package archive is invalid.";
        }

        if (code == ManifestErrorCodes.MissingProperty)
        {
            string? property = ParameterText(error, "property");

            return property is null
                ? "A required manifest property is missing."
                : $"Required manifest property '{property}' is missing.";
        }

        if (code.Value.StartsWith("mod_package.", StringComparison.Ordinal))
        {
            return "The mod package is invalid.";
        }

        return code.Value.StartsWith("manifest.", StringComparison.Ordinal)
            ? "The mod manifest is invalid."
            : "The operation failed.";
    }

    private static IEnumerable<KeyValuePair<string, object?>> VisibleParameters(OperationError error)
    {
        if (error.Code == ManifestErrorCodes.MissingProperty)
        {
            return [];
        }

        return error.Parameters.Where(parameter =>
            parameter.Key is not "instance_path" and not "schema_path" and not "keyword");
    }

    private static string? ParameterText(OperationError error, string key)
    {
        return error.Parameters.TryGetValue(key, out object? value) ? value?.ToString() : null;
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "<null>",
            string text => text,
            IEnumerable values => string.Join(", ", values.Cast<object?>().Select(FormatValue)),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
