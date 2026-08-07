using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Features.Recovery;

public sealed record PreviewRecoveryRequest(string GameDirectory) :
    IRequest<OperationResult<RepositoryRecoveryPreview>>;

public sealed record RecoverRecoveryRequest(string GameDirectory) :
    IRequest<OperationResult<RepositoryRecoveryReport>>;
