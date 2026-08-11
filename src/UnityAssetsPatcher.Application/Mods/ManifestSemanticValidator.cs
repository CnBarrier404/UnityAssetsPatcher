using System.Text.Json;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

internal static class ManifestSemanticValidator
{
    public static OperationError? Validate(ManifestDocumentDto document)
    {
        ArgumentNullException.ThrowIfNull(document);

        OperationError? error = ValidateFiles(document.CopyFiles ?? [], "copyFiles.source") ??
                                ValidateTargets(document.Targets ?? []);

        if (error is not null)
        {
            return error;
        }

        var optionalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ManifestOptionalGroupDto group in document.Optional ?? [])
        {
            if (!optionalNames.Add(group.Name))
            {
                return Error(ManifestErrorCodes.DuplicateOptionalGroup, ("name", group.Name));
            }

            error = ValidateFiles(group.CopyFiles ?? [], "copyFiles.source") ?? ValidateTargets(group.Targets ?? []);

            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static OperationError? ValidateFiles(IEnumerable<ManifestFileDto> files, string propertyName)
    {
        return files.Select(file => ValidateZipRelativePath(file.Source, propertyName)).OfType<OperationError>()
            .FirstOrDefault();
    }

    private static OperationError? ValidateTargets(IEnumerable<ManifestTargetDto> targets)
    {
        foreach (ManifestTargetDto target in targets)
        {
            OperationError? error = ValidateTargetFileName(target.File);

            if (error is not null)
            {
                return error;
            }

            foreach (ManifestPatchDto patch in target.Patches ?? [])
            {
                error = ValidatePatch(patch);

                if (error is not null)
                {
                    return error;
                }
            }
        }

        return null;
    }

    private static OperationError? ValidatePatch(ManifestPatchDto patch)
    {
        OperationError? error = ValidateFieldValueMap(patch.Match, "patch.match");

        if (error is not null)
        {
            return error;
        }

        if (patch.ReplaceAsset is not null)
        {
            error = ValidateZipRelativePath(patch.ReplaceAsset.FromFile, "replaceAsset.fromFile");

            if (error is not null)
            {
                return error;
            }
        }

        if (patch.CopyAsset is not null)
        {
            return ValidateFieldValueMap(
                patch.CopyAsset.From.Match,
                "patch.copy_asset.from.match");
        }

        return null;
    }

    private static OperationError? ValidateFieldValueMap(JsonElement element, string owner)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        return (from property in element.EnumerateObject()
                where !names.Add(property.Name)
                select Error(ManifestErrorCodes.DuplicateProperty, ("owner", owner), ("property", property.Name)))
            .FirstOrDefault();
    }

    private static OperationError? ValidateTargetFileName(string fileName)
    {
        if (Path.IsPathRooted(fileName) ||
            fileName.Contains('/', StringComparison.Ordinal) ||
            fileName.Contains('\\', StringComparison.Ordinal) ||
            fileName is "." or ".." ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return Error(
                ManifestErrorCodes.InvalidPath,
                ("property", "targets.file"),
                ("path", fileName),
                ("expected", "file_name"));
        }

        return null;
    }

    private static OperationError? ValidateZipRelativePath(string path, string propertyName)
    {
        string normalizedPath = path.Replace('\\', '/');

        if (Path.IsPathRooted(path) || normalizedPath.StartsWith("/", StringComparison.Ordinal))
        {
            return Error(
                ManifestErrorCodes.InvalidPath,
                ("property", propertyName),
                ("path", path),
                ("expected", "relative_zip_path"));
        }

        string[] segments = normalizedPath.Split('/');

        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            return Error(
                ManifestErrorCodes.InvalidPath,
                ("property", propertyName),
                ("path", path),
                ("expected", "relative_zip_path_without_navigation"));
        }

        return null;
    }

    private static OperationError Error(
        OperationErrorCode code,
        params (string Key, object? Value)[] parameters)
    {
        return new OperationError(
            code,
            parameters.ToDictionary(parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal));
    }
}
