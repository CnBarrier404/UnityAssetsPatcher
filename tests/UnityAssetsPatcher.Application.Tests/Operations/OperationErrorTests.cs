using System.Collections.ObjectModel;
using UnityAssetsPatcher.Application.Operations;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Operations;

public sealed class OperationErrorTests
{
    [Fact]
    public void Constructor_WhenParametersAreOmitted_UsesEmptyParameters()
    {
        var error = new OperationError(new OperationErrorCode("mod_package.empty"));

        Assert.Empty(error.Parameters);
    }

    [Fact]
    public void Constructor_WhenValuesAreProvided_PreservesStructuredError()
    {
        var errorCode = new OperationErrorCode("mod_package.empty");
        var error = new OperationError(
            errorCode,
            new Dictionary<string, object?>
            {
                ["packagePath"] = @"D:\Mods\example.zip",
            });

        Assert.Equal(errorCode, error.Code);
        Assert.Equal(@"D:\Mods\example.zip", error.Parameters["packagePath"]);
    }

    [Fact]
    public void Constructor_WhenParametersAreChangedLater_PreservesOriginalValues()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["packagePath"] = "original.zip",
        };
        var error = new OperationError(
            new OperationErrorCode("mod_package.invalid_archive"),
            parameters);

        parameters["packagePath"] = "changed.zip";

        Assert.Equal("original.zip", error.Parameters["packagePath"]);
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
    public void Constructor_WhenErrorCodeIsUnknown_PreservesCode()
    {
        var code = new OperationErrorCode("future_feature.new_error");

        var error = new OperationError(code);

        Assert.Same(code, error.Code);
    }
}
