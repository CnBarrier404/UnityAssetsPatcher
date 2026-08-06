namespace UnityAssetsPatcher.Application.Messaging;

public interface IRequestDispatcher
{
    public Task<TResponse> DispatchAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
