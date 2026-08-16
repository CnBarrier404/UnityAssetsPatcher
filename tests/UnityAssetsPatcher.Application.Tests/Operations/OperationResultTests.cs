using UnityAssetsPatcher.Application.Operations;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Operations;

public sealed class OperationResultTests
{
    [Fact]
    public void OperationSucceeded_WhenCreated_PreservesValue()
    {
        var result = new OperationSucceeded<string>("manifest");

        Assert.Equal("manifest", result.Value);
    }

    [Fact]
    public void OperationFailed_WhenCreated_PreservesError()
    {
        var error = new OperationError(new OperationErrorCode("mod_package.invalid_archive"));

        var result = new OperationFailed<string>(error);

        Assert.Same(error, result.Error);
    }

    [Fact]
    public void OperationFailed_WhenErrorIsNull_ThrowsArgumentNullException()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() => new OperationFailed<string>(null!));

        Assert.Equal("error", exception.ParamName);
    }
}
