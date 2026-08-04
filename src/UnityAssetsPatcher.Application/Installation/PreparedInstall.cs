using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Installation;

public sealed record PreparedInstall(
    string ZipFilePath,
    string? GameDirectory,
    IReadOnlyList<string> SelectedOptionalGroups,
    InstallAnalysis Analysis,
    FileIntegrity PackageIntegrity,
    IReadOnlyList<PreparedInstallAssetFile> AssetFiles,
    IReadOnlyDictionary<string, string> ReplacementSourcePaths);

public sealed record PreparedInstallAssetFile(string Path, FileIntegrity Integrity);

public sealed class InstallPreparationStaleException : InvalidOperationException
{
    public InstallPreparationStaleException(string message) : base(message) { }
}
