using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace UnityAssetsPatcher.Application.Messaging;

public sealed class RequestDispatcher : IRequestDispatcher
{
    private static readonly MethodInfo DispatchMethod =
        typeof(RequestDispatcher).GetMethod(
            nameof(DispatchCoreAsync),
            BindingFlags.NonPublic | BindingFlags.Static) ??
        throw new InvalidOperationException(
            "Request dispatcher implementation is incomplete.");

    private readonly IServiceProvider _serviceProvider;

    public RequestDispatcher(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _serviceProvider = serviceProvider;
    }

    public Task<TResponse> DispatchAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        MethodInfo dispatchMethod = DispatchMethod.MakeGenericMethod(request.GetType(), typeof(TResponse));

        try
        {
            return (Task<TResponse>)dispatchMethod.Invoke(
                null,
                [_serviceProvider, request, cancellationToken])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();

            throw;
        }
    }

    private static Task<TResponse> DispatchCoreAsync<TRequest, TResponse>(
        IServiceProvider serviceProvider,
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        return handler.HandleAsync(request, cancellationToken);
    }
}
