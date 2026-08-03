using UnityAssetsPatcher.Application.Operations;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Operations;

public sealed class OperationCodeTests
{
    [Fact]
    public void OperationErrorCode_WhenValuesMatch_HasValueEquality()
    {
        var first = new OperationErrorCode("mod_package.empty");
        var second = new OperationErrorCode("mod_package.empty");

        Assert.Equal(first, second);
        Assert.Equal("mod_package.empty", first.Value);
        Assert.Equal("mod_package.empty", first.ToString());
    }

    [Fact]
    public void OperationAdviceCode_WhenValuesMatch_HasValueEquality()
    {
        var first = new OperationAdviceCode("recovery.run_now");
        var second = new OperationAdviceCode("recovery.run_now");

        Assert.Equal(first, second);
        Assert.Equal("recovery.run_now", first.Value);
        Assert.Equal("recovery.run_now", first.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("empty")]
    [InlineData("ModPackage.Empty")]
    [InlineData("mod-package.empty")]
    [InlineData("mod_package.")]
    [InlineData("mod_package..empty")]
    public void OperationErrorCode_WhenValueHasInvalidFormat_ThrowsArgumentException(string value)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new OperationErrorCode(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void OperationError_WhenCodeIsNull_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new OperationError(null!));

        Assert.Equal("code", exception.ParamName);
    }

    [Fact]
    public void OperationAdvice_WhenCodeIsNull_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new OperationAdvice(null!));

        Assert.Equal("code", exception.ParamName);
    }
}
