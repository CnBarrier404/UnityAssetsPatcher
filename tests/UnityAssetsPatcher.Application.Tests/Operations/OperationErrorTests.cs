using System.Collections.ObjectModel;
using UnityAssetsPatcher.Application.Operations;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Operations;

public sealed class OperationErrorTests
{
    [Fact]
    public void Constructor_WhenOptionalCollectionsAreOmitted_UsesEmptyCollections()
    {
        var error = new OperationError(new OperationErrorCode("mod_package.empty"));

        Assert.Empty(error.Parameters);
        Assert.Empty(error.Advice);
    }

    [Fact]
    public void Constructor_WhenValuesAreProvided_PreservesStructuredError()
    {
        var errorCode = new OperationErrorCode("mod_package.empty");
        var adviceCode = new OperationAdviceCode("mod_package.download_again");
        var advice = new OperationAdvice(
            adviceCode,
            new Dictionary<string, object?>
            {
                ["source"] = "official-site",
            });
        var error = new OperationError(
            errorCode,
            new Dictionary<string, object?>
            {
                ["packagePath"] = @"D:\Mods\example.zip",
            },
            [advice]);

        Assert.Equal(errorCode, error.Code);
        Assert.Equal(@"D:\Mods\example.zip", error.Parameters["packagePath"]);
        OperationAdvice actualAdvice = Assert.Single(error.Advice);
        Assert.Equal(adviceCode, actualAdvice.Code);
        Assert.Equal("official-site", actualAdvice.Parameters["source"]);
    }

    [Fact]
    public void Constructor_WhenInputsAreChangedLater_PreservesOriginalValues()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["packagePath"] = "original.zip",
        };
        var originalAdvice = new OperationAdvice(new OperationAdviceCode("mod_package.check_integrity"));
        var advice = new List<OperationAdvice>
        {
            originalAdvice,
        };
        var error = new OperationError(
            new OperationErrorCode("mod_package.invalid_archive"),
            parameters,
            advice);

        parameters["packagePath"] = "changed.zip";
        advice[0] = new OperationAdvice(new OperationAdviceCode("mod_package.download_again"));

        Assert.Equal("original.zip", error.Parameters["packagePath"]);
        Assert.Same(originalAdvice, Assert.Single(error.Advice));
    }

    [Fact]
    public void Parameters_WhenMutationIsAttempted_ThrowsNotSupportedException()
    {
        var error = new OperationError(
            new OperationErrorCode("file.not_found"),
            new Dictionary<string, object?>
            {
                ["path"] = "manifest.json",
            });
        var parameters = Assert.IsType<ReadOnlyDictionary<string, object?>>(error.Parameters);
        IDictionary<string, object?> mutableView = parameters;

        Assert.Throws<NotSupportedException>(() => mutableView["path"] = "changed.json");
    }

    [Fact]
    public void Constructor_WhenParameterNameIsBlank_ThrowsArgumentException()
    {
        var parameters = new Dictionary<string, object?>
        {
            [" "] = "value",
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new OperationError(new OperationErrorCode("manifest.invalid"), parameters));

        Assert.Equal("parameters", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenAdviceContainsNull_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new OperationError(new OperationErrorCode("manifest.invalid"), advice: [null!]));

        Assert.Equal("advice", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenErrorCodeIsUnknown_PreservesCode()
    {
        var code = new OperationErrorCode("future_feature.new_error");

        var error = new OperationError(code);

        Assert.Same(code, error.Code);
    }

    [Fact]
    public void OperationAdvice_WhenAdviceCodeIsUnknown_PreservesCode()
    {
        var code = new OperationAdviceCode("future_feature.try_something_new");

        var advice = new OperationAdvice(code);

        Assert.Same(code, advice.Code);
    }
}
