namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InspectListRequest(string AssetsFilePath, int? Limit);

public sealed record InspectFieldsRequest(string AssetsFilePath, long PathId);

public sealed record UninstallModRequest(string InstallId, string? GameDirectory = null);

public sealed record UninstallPreviewRequest(string InstallId, string? GameDirectory = null);
