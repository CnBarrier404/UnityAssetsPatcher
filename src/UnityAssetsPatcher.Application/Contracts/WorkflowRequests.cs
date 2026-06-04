namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InspectListRequest(string AssetsFilePath, int? Limit);

public sealed record InspectFieldsRequest(string AssetsFilePath, long PathId);

public sealed record FindAssetsRequest(string AssetsFilePath, string ConfigPath);

public sealed record PatchPreviewRequest(string AssetsFilePath, string ConfigPath);

public sealed record PatchApplyRequest(
    string AssetsFilePath,
    string ConfigPath,
    string? OutputPath,
    string BackupDirectory);

public sealed record InstallModRequest(
    string ZipFilePath,
    string GameDirectory,
    string BackupDirectory);

public sealed record InstallPreviewRequest(string ZipFilePath, string GameDirectory);
