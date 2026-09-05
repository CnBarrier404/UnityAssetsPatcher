using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Updates;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Updates;

public sealed class UpdateCheckModuleTests
{
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task CheckForUpdateAsync_WhenCheckerReturnsUpdate_ReturnsSuccess()
    {
        UpdateInfo update = Manifest("v1.3.0");
        UpdateCheckModule module = CreateOperation(new StubUpdateChecker(update));

        var result = Assert.IsType<OperationSucceeded<UpdateInfo?>>(await Check(module));

        Assert.Same(update, result.Value);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenCheckerReturnsNull_ReturnsSuccessfulNoUpdate()
    {
        UpdateCheckModule module = CreateOperation(new StubUpdateChecker((UpdateInfo?)null));

        var result = Assert.IsType<OperationSucceeded<UpdateInfo?>>(await Check(module));

        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException), "Offline")]
    [InlineData(typeof(IOException), "Read failed")]
    [InlineData(typeof(JsonException), "Invalid JSON")]
    public async Task CheckForUpdateAsync_WhenExpectedExceptionIsThrown_ReturnsFailure(
        Type exceptionType,
        string message)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;
        UpdateCheckModule module = CreateOperation(new StubUpdateChecker(exception));

        var result = Assert.IsType<OperationFailed<UpdateInfo?>>(await Check(module));

        Assert.Equal(UpdateErrorCodes.CheckFailed, result.Error.Code);
        Assert.Equal(message, result.Error.Parameters["detail"]);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenNonCallerCancellationIsThrown_PropagatesCancellation()
    {
        UpdateCheckModule module = CreateOperation(new StubUpdateChecker(new OperationCanceledException()));

        await Assert.ThrowsAsync<OperationCanceledException>(() => Check(module));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenRequestTimesOut_ReturnsFailure()
    {
        var exception = new OperationCanceledException(
            "Request timed out.",
            new TimeoutException("Request timed out."));
        UpdateCheckModule module = CreateOperation(new StubUpdateChecker(exception));

        var result = Assert.IsType<OperationFailed<UpdateInfo?>>(await Check(module));

        Assert.Equal(UpdateErrorCodes.CheckFailed, result.Error.Code);
        Assert.Equal(exception.Message, result.Error.Parameters["detail"]);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenUnexpectedExceptionIsThrown_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected");
        UpdateCheckModule module = CreateOperation(new StubUpdateChecker(exception));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Check(module));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenCallerCancellationIsRequested_PropagatesCancellation()
    {
        var checker = new StubUpdateChecker(Manifest("v1.3.0"));
        UpdateCheckModule module = CreateOperation(checker);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            module.CheckForUpdateAsync(cancellation.Token));

        Assert.Equal(0, checker.CallCount);
    }

    private static UpdateCheckModule CreateOperation(IUpdateChecker checker)
    {
        return new UpdateCheckModule(checker, NullLogger<UpdateCheckModule>.Instance);
    }

    private static Task<OperationResult<UpdateInfo?>> Check(UpdateCheckModule module)
    {
        return module.CheckForUpdateAsync(TestContext.Current.CancellationToken);
    }

    private static UpdateInfo Manifest(string version)
    {
        return new UpdateInfo(
            version,
            new Uri($"https://example.com/releases/{version}"),
            new Uri($"https://example.com/download/{version}"),
            Sha256);
    }

    private sealed class StubUpdateChecker : IUpdateChecker
    {
        private readonly UpdateInfo? _update;
        private readonly Exception? _exception;

        public int CallCount { get; private set; }

        public StubUpdateChecker(UpdateInfo? update)
        {
            _update = update;
        }

        public StubUpdateChecker(Exception exception)
        {
            _exception = exception;
        }

        public Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;

            return _exception is null
                ? Task.FromResult(_update)
                : Task.FromException<UpdateInfo?>(_exception);
        }
    }
}
