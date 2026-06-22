namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InstallModRequest(string ZipFilePath, string? GameDirectory, string BackupDirectory)
{
    public IReadOnlyList<string> SelectedOptionalGroups { get; init; } = [];
}

public sealed record InstallPreviewRequest(string ZipFilePath, string? GameDirectory)
{
    public IReadOnlyList<string> SelectedOptionalGroups { get; init; } = [];
}

public sealed record UninstallModRequest(string InstallDirectory);

public sealed record UninstallPreviewRequest(string InstallDirectory);
