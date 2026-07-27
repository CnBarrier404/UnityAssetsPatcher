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
            OperationErrorCode.FileNotFound => FormatContext(LocalizedStrings.Error_FileNotFoundFormat, context),
            OperationErrorCode.DirectoryNotFound =>
                FormatContext(LocalizedStrings.Error_DirectoryNotFoundFormat, context),
            OperationErrorCode.AccessDenied => FormatContext(LocalizedStrings.Error_AccessDeniedFormat, context),
            OperationErrorCode.FileSystemFailure =>
                FormatContext(LocalizedStrings.Error_FileSystemFailureFormat, context),
            OperationErrorCode.InvalidManifest => LocalizedStrings.Error_InvalidManifest,
            OperationErrorCode.UnsupportedManifestVersion => LocalizedStrings.Error_UnsupportedManifestVersion,
            OperationErrorCode.InvalidModPackage => LocalizedStrings.Error_InvalidModPackage,
            OperationErrorCode.GameDirectoryRequired => LocalizedStrings.Error_GameDirectoryRequired,
            OperationErrorCode.GameDirectoryNotFound =>
                FormatContext(LocalizedStrings.Error_GameDirectoryNotFoundFormat, context),
            OperationErrorCode.AssetNotFound => LocalizedStrings.Error_AssetNotFound,
            OperationErrorCode.PatchPlanningFailed => LocalizedStrings.Error_PatchPlanningFailed,
            OperationErrorCode.InstallRecordNotFound => LocalizedStrings.Error_InstallRecordNotFound,
            OperationErrorCode.FileIntegrityMismatch => LocalizedStrings.Error_FileIntegrityMismatch,
            OperationErrorCode.OperationAlreadyRunning => LocalizedStrings.Error_OperationAlreadyRunning,
            OperationErrorCode.RecoveryRequired => LocalizedStrings.Error_RecoveryRequired,
            OperationErrorCode.BackupRepositoryUnsafe => LocalizedStrings.Error_BackupRepositoryUnsafe,
            OperationErrorCode.UnsupportedBackupRepositoryVersion =>
                LocalizedStrings.Error_UnsupportedBackupRepositoryVersion,
            _ => LocalizedStrings.Error_OperationFailed,
        };
    }

    public static string FormatUnexpected()
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnityAssetsPatcher",
            "logs");

        return string.Format(LocalizedStrings.Error_UnexpectedFormat, logDirectory);
    }

    public static string Format(PatchDiagnostic diagnostic)
    {
        return diagnostic.Code switch
        {
            PatchDiagnosticCode.InvalidPatchConfiguration => LocalizedStrings.PatchError_InvalidConfiguration,
            PatchDiagnosticCode.NoMatchingAssets => LocalizedStrings.PatchError_NoMatchingAssets,
            PatchDiagnosticCode.FieldNotFound =>
                string.Format(LocalizedStrings.PatchError_FieldNotFoundFormat, diagnostic.FieldPath),
            PatchDiagnosticCode.ValueMismatch => string.Format(
                LocalizedStrings.PatchError_ValueMismatchFormat,
                diagnostic.FieldPath,
                diagnostic.Expected,
                diagnostic.Actual),
            PatchDiagnosticCode.UnsupportedValue => LocalizedStrings.PatchError_UnsupportedValue,
            PatchDiagnosticCode.PathIdReferenceNotFound => LocalizedStrings.PatchError_PathIdReferenceNotFound,
            PatchDiagnosticCode.PathIdReferenceAmbiguous => LocalizedStrings.PatchError_PathIdReferenceAmbiguous,
            PatchDiagnosticCode.ReplacementSourceNotFound => LocalizedStrings.PatchError_ReplacementSourceNotFound,
            PatchDiagnosticCode.ReplacementMatchInvalid => LocalizedStrings.PatchError_ReplacementMatchInvalid,
            _ => LocalizedStrings.Error_OperationFailed,
        };
    }

    public static string Format(BackupRecoveryIssue issue)
    {
        return issue.Code switch
        {
            BackupRecoveryIssueCode.RepositoryUnsafe => LocalizedStrings.BackupRecovery_RepositoryUnsafe,
            BackupRecoveryIssueCode.RecoveryUnsafe => LocalizedStrings.BackupRecovery_RecoveryUnsafe,
            BackupRecoveryIssueCode.OperationFailed => LocalizedStrings.BackupRecovery_OperationFailed,
            BackupRecoveryIssueCode.UnexpectedFailure => FormatUnexpected(),
            _ => LocalizedStrings.Error_OperationFailed,
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
