using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Features.Inspect;

public sealed record InspectListRequest(string AssetsFilePath, int? Limit) :
    IRequest<OperationResult<InspectListResult>>;

public sealed record InspectFieldsRequest(string AssetsFilePath, long PathId) :
    IRequest<OperationResult<AssetField>>;

public sealed record InspectAssetSummary(long PathId, string TypeName, string? Name);

public sealed record InspectListResult(IReadOnlyList<InspectAssetSummary> Assets, int TotalCount);
