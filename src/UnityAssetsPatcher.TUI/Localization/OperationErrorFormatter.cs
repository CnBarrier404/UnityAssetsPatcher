using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Workflows;

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
            _ when error.Code == WorkflowErrorCodes.GameDirectoryRequired =>
                strings.Error_GameDirectoryRequired,
            _ when error.Code == WorkflowErrorCodes.GameDirectoryNotFound =>
                FormatContext(strings.Error_GameDirectoryNotFoundFormat, context),
            _ when error.Code == WorkflowErrorCodes.AssetNotFound => strings.Error_AssetNotFound,
            _ when error.Code == WorkflowErrorCodes.PatchPlanningFailed =>
                strings.Error_PatchPlanningFailed,
            _ when error.Code == WorkflowErrorCodes.InstallRecordNotFound =>
                strings.Error_InstallRecordNotFound,
            _ when error.Code == WorkflowErrorCodes.FileIntegrityMismatch =>
                strings.Error_FileIntegrityMismatch,
            _ when error.Code == WorkflowErrorCodes.InstallPreviewStale =>
                strings.Error_InstallPreviewStale,
            _ when error.Code == WorkflowErrorCodes.OperationAlreadyRunning =>
                strings.Error_OperationAlreadyRunning,
            _ when error.Code == WorkflowErrorCodes.RecoveryRequired => strings.Error_RecoveryRequired,
            _ when error.Code == WorkflowErrorCodes.BackupRepositoryUnsafe =>
                strings.Error_BackupRepositoryUnsafe,
            _ when error.Code == WorkflowErrorCodes.UnsupportedBackupRepositoryVersion =>
                strings.Error_UnsupportedBackupRepositoryVersion,
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

    public static string Format(LocalizedStrings strings, BackupRecoveryIssue issue)
    {
        return issue.Code switch
        {
            BackupRecoveryIssueCode.RepositoryUnsafe => strings.BackupRecovery_RepositoryUnsafe,
            BackupRecoveryIssueCode.RecoveryUnsafe => strings.BackupRecovery_RecoveryUnsafe,
            BackupRecoveryIssueCode.OperationFailed => strings.BackupRecovery_OperationFailed,
            BackupRecoveryIssueCode.UnexpectedFailure => FormatUnexpected(strings),
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
