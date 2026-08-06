using UnityAssetsPatcher.Application.Messaging;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Messaging;

public sealed class RequestDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WhenHandlerIsRegistered_ReturnsHandlerResponseAndForwardsRequest()
    {
        var handler = new TestRequestHandler();
        var dispatcher = new RequestDispatcher(new StubServiceProvider(handler));
        var request = new TestRequest("request");
        using CancellationTokenSource cancellation = new();

        TestResponse response = await dispatcher
            .DispatchAsync<TestRequest, TestResponse>(request, cancellation.Token);

        Assert.Equal("request", response.Value);
        Assert.Same(request, handler.Request);
        Assert.Equal(cancellation.Token, handler.CancellationToken);
    }

    [Fact]
    public async Task DispatchAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var dispatcher = new RequestDispatcher(new StubServiceProvider(null));

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dispatcher.DispatchAsync<TestRequest, TestResponse>(null!, TestContext.Current.CancellationToken));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerIsNotRegistered_ThrowsInvalidOperationException()
    {
        var dispatcher = new RequestDispatcher(new StubServiceProvider(null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync<TestRequest, TestResponse>(
                new TestRequest("request"), TestContext.Current.CancellationToken));
    }

    private sealed record TestRequest(string Value) : IRequest<TestResponse>;

    private sealed record TestResponse(string Value);

    private sealed class TestRequestHandler : IRequestHandler<TestRequest, TestResponse>
    {
        public TestRequest? Request { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<TestResponse> HandleAsync(
            TestRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            CancellationToken = cancellationToken;

            TestResponse response = new(request.Value);

            return Task.FromResult(response);
        }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly object? _handler;

        public StubServiceProvider(object? handler)
        {
            _handler = handler;
        }

        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(IRequestHandler<TestRequest, TestResponse>)
                ? _handler
                : null;
        }
    }
}
