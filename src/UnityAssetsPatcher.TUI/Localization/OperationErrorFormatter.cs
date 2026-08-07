using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.TUI.Localization;

internal static class OperationErrorFormatter
{
    public static string Format(LocalizedStrings strings, OperationError error)
    {
        string context = ParameterText(error, "path") ?? string.Empty;

        if (error.Code == FileErrorCodes.NotFound)
        {
            return FormatContext(strings.Error_FileNotFoundFormat, context);
        }

        if (error.Code == FileErrorCodes.DirectoryNotFound)
        {
            return FormatContext(strings.Error_DirectoryNotFoundFormat, context);
        }

        if (error.Code == FileErrorCodes.AccessDenied)
        {
            return FormatContext(strings.Error_AccessDeniedFormat, context);
        }

        if (error.Code == FileErrorCodes.ReadFailed || error.Code == FileErrorCodes.SystemFailure)
        {
            return FormatContext(strings.Error_FileSystemFailureFormat, context);
        }

        if (error.Code == ManifestErrorCodes.InvalidManifest ||
            error.Code == ManifestErrorCodes.InvalidJson ||
            error.Code == ManifestErrorCodes.InvalidPropertyType ||
            error.Code == ManifestErrorCodes.InvalidValue ||
            error.Code == ManifestErrorCodes.MissingProperty)
        {
            return strings.Error_InvalidManifest;
        }

        if (error.Code == ManifestErrorCodes.UnsupportedSchema)
        {
            return strings.Error_UnsupportedManifestVersion;
        }

        if (error.Code.Value.StartsWith("mod_package.", StringComparison.Ordinal))
        {
            return strings.Error_InvalidModPackage;
        }

        return error.Code switch
        {
            _ when error.Code == GameDirectoryErrorCodes.Required =>
                strings.Error_GameDirectoryRequired,
            _ when error.Code == GameDirectoryErrorCodes.NotFound =>
                FormatContext(strings.Error_GameDirectoryNotFoundFormat, context),
            _ when error.Code == AssetErrorCodes.NotFound => strings.Error_AssetNotFound,
            _ when error.Code == PatchErrorCodes.PlanningFailed =>
                strings.Error_PatchPlanningFailed,
            _ when error.Code == ModOperationErrorCodes.InstallRecordNotFound =>
                strings.Error_InstallRecordNotFound,
            _ when error.Code == ModOperationErrorCodes.FileIntegrityMismatch =>
                strings.Error_FileIntegrityMismatch,
            _ when error.Code == ModOperationErrorCodes.InstallPreviewStale =>
                strings.Error_InstallPreviewStale,
            _ when error.Code == RepositoryErrorCodes.OperationAlreadyRunning =>
                strings.Error_OperationAlreadyRunning,
            _ when error.Code == RepositoryErrorCodes.RecoveryRequired => strings.Error_RecoveryRequired,
            _ when error.Code == RepositoryErrorCodes.Unsafe =>
                strings.Error_RepositoryUnsafe,
            _ when error.Code == RepositoryErrorCodes.UnsupportedVersion =>
                strings.Error_UnsupportedRepositoryVersion,
            _ => strings.Error_OperationFailed,
        };
    }

    public static string FormatUnexpected(LocalizedStrings strings)
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnityAssetsPatcher",
            "logs");

        return strings.Error_UnexpectedFormat(logDirectory);
    }

    public static string Format(LocalizedStrings strings, PatchDiagnostic diagnostic)
    {
        return diagnostic.Code switch
        {
            PatchDiagnosticCode.InvalidPatchConfiguration => strings.PatchError_InvalidConfiguration,
            PatchDiagnosticCode.NoMatchingAssets => strings.PatchError_NoMatchingAssets,
            PatchDiagnosticCode.FieldNotFound =>
                strings.PatchError_FieldNotFoundFormat(diagnostic.FieldPath),
            PatchDiagnosticCode.ValueMismatch => strings.PatchError_ValueMismatchFormat(
                diagnostic.FieldPath,
                diagnostic.Expected,
                diagnostic.Actual),
            PatchDiagnosticCode.UnsupportedValue => strings.PatchError_UnsupportedValue,
            PatchDiagnosticCode.PathIdReferenceNotFound => strings.PatchError_PathIdReferenceNotFound,
            PatchDiagnosticCode.PathIdReferenceAmbiguous => strings.PatchError_PathIdReferenceAmbiguous,
            PatchDiagnosticCode.ReplacementSourceNotFound =>
                strings.PatchError_ReplacementSourceNotFound,
            PatchDiagnosticCode.ReplacementMatchInvalid => strings.PatchError_ReplacementMatchInvalid,
            _ => strings.Error_OperationFailed,
        };
    }

    public static string Format(LocalizedStrings strings, RepositoryRecoveryIssue issue)
    {
        return issue.Code switch
        {
            RepositoryRecoveryIssueCode.RepositoryUnsafe => strings.RepositoryRecovery_RepositoryUnsafe,
            RepositoryRecoveryIssueCode.RecoveryUnsafe => strings.RepositoryRecovery_RecoveryUnsafe,
            RepositoryRecoveryIssueCode.OperationFailed => strings.RepositoryRecovery_OperationFailed,
            RepositoryRecoveryIssueCode.UnexpectedFailure => FormatUnexpected(strings),
            _ => strings.Error_OperationFailed,
        };
    }

    private static string FormatContext(Func<object?, string> format, string context)
    {
        string text = format(context);

        return string.IsNullOrWhiteSpace(context)
            ? text.TrimEnd().TrimEnd(':', '：')
            : text;
    }

    private static string? ParameterText(OperationError error, string key)
    {
        return error.Parameters.TryGetValue(key, out object? value) ? value?.ToString() : null;
    }
}
