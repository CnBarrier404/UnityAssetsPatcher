namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InstallModRequest(string ZipFilePath, string? GameDirectory, string BackupDirectory);

public sealed record InstallPreviewRequest(string ZipFilePath, string? GameDirectory);
