using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.Application.Features.RepositoryManagement;

public sealed record ClearUnsupportedRepositoryRequest :
    IRequest<OperationResult<RepositoryClearResult>>;
