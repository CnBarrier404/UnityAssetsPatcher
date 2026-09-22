using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Updates;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Updates;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_WhenReleaseIsNewer_ReturnsUpdate()
    {
        UpdateInfo update = Manifest("v1.3.0");
        UpdateCheckService service = CreateOperation(new StubUpdateChecker(update));

        UpdateInfo? result = await Check(service);

        Assert.Same(update, result);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenReleaseIsNotNewer_ReturnsSuccessfulNoUpdate()
    {
        UpdateCheckService service = CreateOperation(new StubUpdateChecker(Manifest("v1.2.3")));

        UpdateInfo? result = await Check(service);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException), "Offline")]
    [InlineData(typeof(IOException), "Read failed")]
    [InlineData(typeof(JsonException), "Invalid JSON")]
    public async Task CheckForUpdateAsync_WhenExpectedExceptionIsThrown_ThrowsUpdateCheckException(
        Type exceptionType,
        string message)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;
        UpdateCheckService service = CreateOperation(new StubUpdateChecker(exception));

        var result = await Assert.ThrowsAsync<UpdateCheckException>(() => Check(service));

        Assert.Same(exception, result.InnerException);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenNonCallerCancellationIsThrown_PropagatesCancellation()
    {
        UpdateCheckService service = CreateOperation(new StubUpdateChecker(new OperationCanceledException()));

        await Assert.ThrowsAsync<OperationCanceledException>(() => Check(service));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenRequestTimesOut_ThrowsUpdateCheckException()
    {
        var exception = new OperationCanceledException(
            "Request timed out.",
            new TimeoutException("Request timed out."));
        UpdateCheckService service = CreateOperation(new StubUpdateChecker(exception));

        var result = await Assert.ThrowsAsync<UpdateCheckException>(() => Check(service));

        Assert.Same(exception, result.InnerException);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenUnexpectedExceptionIsThrown_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected");
        UpdateCheckService service = CreateOperation(new StubUpdateChecker(exception));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Check(service));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenCallerCancellationIsRequested_PropagatesCancellation()
    {
        var releaseClient = new StubUpdateChecker(Manifest("v1.3.0"));
        UpdateCheckService service = CreateOperation(releaseClient);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CheckForUpdateAsync(cancellation.Token));

        Assert.Equal(0, releaseClient.CallCount);
    }

    private static UpdateCheckService CreateOperation(IUpdateChecker releaseClient)
    {
        return new UpdateCheckService(
            releaseClient,
            "v1.2.3",
            NullLogger<UpdateCheckService>.Instance);
    }

    private static Task<UpdateInfo?> Check(UpdateCheckService service)
    {
        return service.CheckForUpdateAsync(TestContext.Current.CancellationToken);
    }

    private static UpdateInfo Manifest(string version)
    {
        return new UpdateInfo(
            version,
            new Uri($"https://example.com/releases/{version}"));
    }

    private sealed class StubUpdateChecker : IUpdateChecker
    {
        private readonly UpdateInfo? _release;
        private readonly Exception? _exception;

        public int CallCount { get; private set; }

        public StubUpdateChecker(UpdateInfo release)
        {
            _release = release;
        }

        public StubUpdateChecker(Exception exception)
        {
            _exception = exception;
        }

        public Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;

            return _exception is null
                ? Task.FromResult(_release!)
                : Task.FromException<UpdateInfo>(_exception);
        }
    }
}
