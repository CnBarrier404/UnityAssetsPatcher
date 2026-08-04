using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.TUI.Localization;

public static class OperationErrorFormatter
{
    public static string Format(OperationError error)
    {
        string context = ParameterText(error, "path") ?? string.Empty;

        if (error.Code == FileErrorCodes.NotFound)
        {
            return FormatContext(LegacyLocalizedStrings.Error_FileNotFoundFormat, context);
        }

        if (error.Code == FileErrorCodes.DirectoryNotFound)
        {
            return FormatContext(LegacyLocalizedStrings.Error_DirectoryNotFoundFormat, context);
        }

        if (error.Code == FileErrorCodes.AccessDenied)
        {
            return FormatContext(LegacyLocalizedStrings.Error_AccessDeniedFormat, context);
        }

        if (error.Code == FileErrorCodes.ReadFailed || error.Code == FileErrorCodes.SystemFailure)
        {
            return FormatContext(LegacyLocalizedStrings.Error_FileSystemFailureFormat, context);
        }

        if (error.Code == ManifestErrorCodes.InvalidManifest ||
            error.Code == ManifestErrorCodes.InvalidJson ||
            error.Code == ManifestErrorCodes.InvalidPropertyType ||
            error.Code == ManifestErrorCodes.InvalidValue ||
            error.Code == ManifestErrorCodes.MissingProperty)
        {
            return LegacyLocalizedStrings.Error_InvalidManifest;
        }

        if (error.Code == ManifestErrorCodes.UnsupportedSchema)
        {
            return LegacyLocalizedStrings.Error_UnsupportedManifestVersion;
        }

        if (error.Code.Value.StartsWith("mod_package.", StringComparison.Ordinal))
        {
            return LegacyLocalizedStrings.Error_InvalidModPackage;
        }

        return error.Code switch
        {
            _ when error.Code == WorkflowErrorCodes.GameDirectoryRequired =>
                LegacyLocalizedStrings.Error_GameDirectoryRequired,
            _ when error.Code == WorkflowErrorCodes.GameDirectoryNotFound =>
                FormatContext(LegacyLocalizedStrings.Error_GameDirectoryNotFoundFormat, context),
            _ when error.Code == WorkflowErrorCodes.AssetNotFound => LegacyLocalizedStrings.Error_AssetNotFound,
            _ when error.Code == WorkflowErrorCodes.PatchPlanningFailed =>
                LegacyLocalizedStrings.Error_PatchPlanningFailed,
            _ when error.Code == WorkflowErrorCodes.InstallRecordNotFound =>
                LegacyLocalizedStrings.Error_InstallRecordNotFound,
            _ when error.Code == WorkflowErrorCodes.FileIntegrityMismatch =>
                LegacyLocalizedStrings.Error_FileIntegrityMismatch,
            _ when error.Code == WorkflowErrorCodes.InstallPreviewStale =>
                LegacyLocalizedStrings.Error_InstallPreviewStale,
            _ when error.Code == WorkflowErrorCodes.OperationAlreadyRunning =>
                LegacyLocalizedStrings.Error_OperationAlreadyRunning,
            _ when error.Code == WorkflowErrorCodes.RecoveryRequired => LegacyLocalizedStrings.Error_RecoveryRequired,
            _ when error.Code == WorkflowErrorCodes.BackupRepositoryUnsafe =>
                LegacyLocalizedStrings.Error_BackupRepositoryUnsafe,
            _ when error.Code == WorkflowErrorCodes.UnsupportedBackupRepositoryVersion =>
                LegacyLocalizedStrings.Error_UnsupportedBackupRepositoryVersion,
            _ => LegacyLocalizedStrings.Error_OperationFailed,
        };
    }

    public static string FormatUnexpected()
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnityAssetsPatcher",
            "logs");

        return string.Format(LegacyLocalizedStrings.Error_UnexpectedFormat, logDirectory);
    }

    public static string Format(PatchDiagnostic diagnostic)
    {
        return diagnostic.Code switch
        {
            PatchDiagnosticCode.InvalidPatchConfiguration => LegacyLocalizedStrings.PatchError_InvalidConfiguration,
            PatchDiagnosticCode.NoMatchingAssets => LegacyLocalizedStrings.PatchError_NoMatchingAssets,
            PatchDiagnosticCode.FieldNotFound =>
                string.Format(LegacyLocalizedStrings.PatchError_FieldNotFoundFormat, diagnostic.FieldPath),
            PatchDiagnosticCode.ValueMismatch => string.Format(
                LegacyLocalizedStrings.PatchError_ValueMismatchFormat,
                diagnostic.FieldPath,
                diagnostic.Expected,
                diagnostic.Actual),
            PatchDiagnosticCode.UnsupportedValue => LegacyLocalizedStrings.PatchError_UnsupportedValue,
            PatchDiagnosticCode.PathIdReferenceNotFound => LegacyLocalizedStrings.PatchError_PathIdReferenceNotFound,
            PatchDiagnosticCode.PathIdReferenceAmbiguous => LegacyLocalizedStrings.PatchError_PathIdReferenceAmbiguous,
            PatchDiagnosticCode.ReplacementSourceNotFound =>
                LegacyLocalizedStrings.PatchError_ReplacementSourceNotFound,
            PatchDiagnosticCode.ReplacementMatchInvalid => LegacyLocalizedStrings.PatchError_ReplacementMatchInvalid,
            _ => LegacyLocalizedStrings.Error_OperationFailed,
        };
    }

    public static string Format(BackupRecoveryIssue issue)
    {
        return issue.Code switch
        {
            BackupRecoveryIssueCode.RepositoryUnsafe => LegacyLocalizedStrings.BackupRecovery_RepositoryUnsafe,
            BackupRecoveryIssueCode.RecoveryUnsafe => LegacyLocalizedStrings.BackupRecovery_RecoveryUnsafe,
            BackupRecoveryIssueCode.OperationFailed => LegacyLocalizedStrings.BackupRecovery_OperationFailed,
            BackupRecoveryIssueCode.UnexpectedFailure => FormatUnexpected(),
            _ => LegacyLocalizedStrings.Error_OperationFailed,
        };
    }

    private static string FormatContext(string format, string context)
    {
        string text = string.Format(format, context);

        return string.IsNullOrWhiteSpace(context)
            ? text.TrimEnd().TrimEnd(':', '：')
            : text;
    }

    private static string? ParameterText(OperationError error, string key)
    {
        return error.Parameters.TryGetValue(key, out object? value) ? value?.ToString() : null;
    }
}
