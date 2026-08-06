namespace UnityAssetsPatcher.Application.Contracts;

public sealed record InspectListRequest(string AssetsFilePath, int? Limit);

public sealed record InspectFieldsRequest(string AssetsFilePath, long PathId);
