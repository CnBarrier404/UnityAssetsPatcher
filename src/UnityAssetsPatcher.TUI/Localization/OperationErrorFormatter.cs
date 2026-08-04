using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.TUI.Localization;

public static class OperationErrorFormatter
{
    public static string Format(OperationError error)
    {
        string context = error.Parameters.GetValueOrDefault("path", string.Empty);

        return error.Code switch
        {
            OperationErrorCode.FileNotFound => FormatContext(LegacyLocalizedStrings.Error_FileNotFoundFormat, context),
            OperationErrorCode.DirectoryNotFound =>
                FormatContext(LegacyLocalizedStrings.Error_DirectoryNotFoundFormat, context),
            OperationErrorCode.AccessDenied => FormatContext(LegacyLocalizedStrings.Error_AccessDeniedFormat, context),
            OperationErrorCode.FileSystemFailure =>
                FormatContext(LegacyLocalizedStrings.Error_FileSystemFailureFormat, context),
            OperationErrorCode.InvalidManifest => LegacyLocalizedStrings.Error_InvalidManifest,
            OperationErrorCode.UnsupportedManifestVersion => LegacyLocalizedStrings.Error_UnsupportedManifestVersion,
            OperationErrorCode.InvalidModPackage => LegacyLocalizedStrings.Error_InvalidModPackage,
            OperationErrorCode.GameDirectoryRequired => LegacyLocalizedStrings.Error_GameDirectoryRequired,
            OperationErrorCode.GameDirectoryNotFound =>
                FormatContext(LegacyLocalizedStrings.Error_GameDirectoryNotFoundFormat, context),
            OperationErrorCode.AssetNotFound => LegacyLocalizedStrings.Error_AssetNotFound,
            OperationErrorCode.PatchPlanningFailed => LegacyLocalizedStrings.Error_PatchPlanningFailed,
            OperationErrorCode.InstallRecordNotFound => LegacyLocalizedStrings.Error_InstallRecordNotFound,
            OperationErrorCode.FileIntegrityMismatch => LegacyLocalizedStrings.Error_FileIntegrityMismatch,
            OperationErrorCode.InstallPreviewStale => LegacyLocalizedStrings.Error_InstallPreviewStale,
            OperationErrorCode.OperationAlreadyRunning => LegacyLocalizedStrings.Error_OperationAlreadyRunning,
            OperationErrorCode.RecoveryRequired => LegacyLocalizedStrings.Error_RecoveryRequired,
            OperationErrorCode.BackupRepositoryUnsafe => LegacyLocalizedStrings.Error_BackupRepositoryUnsafe,
            OperationErrorCode.UnsupportedBackupRepositoryVersion =>
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
}
