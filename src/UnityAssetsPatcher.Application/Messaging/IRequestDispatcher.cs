namespace UnityAssetsPatcher.Application.Messaging;

public interface IRequestDispatcher
{
    public Task<TResponse> DispatchAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>;
}
