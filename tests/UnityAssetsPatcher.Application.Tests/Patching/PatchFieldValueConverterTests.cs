using System.Text.Json;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Patching.Fields;
using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Patching;

public sealed class PatchFieldValueConverterTests
{
    [Theory]
    [InlineData("123")]
    [InlineData("true")]
    public void EnsureCompatiblePatchValue_WhenStringTargetReceivesNonStringValue_Throws(string value)
    {
        var target = new AssetScalarField(
            "m_Name",
            "string",
            new AssetScalarValue.String("Text"));

        var exception = Assert.Throws<PatchPlanningException>(() =>
            PatchFieldValueConverter.EnsureCompatiblePatchValue(
                target,
                JsonValue(value),
                "m_Name"));

        Assert.Equal(PatchDiagnosticCode.InvalidPatchConfiguration, exception.Diagnostic.Code);
        Assert.Equal("m_Name", exception.Diagnostic.FieldPath);
        Assert.Contains("String", exception.Diagnostic.Expected);
    }

    [Fact]
    public void EnsureCompatiblePatchValue_WhenArrayElementTypeDoesNotMatch_ThrowsWithElementPath()
    {
        var target = new AssetArrayField(
            "numbers",
            "vector",
            new AssetScalarFieldSchema("int", AssetScalarKind.Int32),
            []);

        var exception = Assert.Throws<PatchPlanningException>(() =>
            PatchFieldValueConverter.EnsureCompatiblePatchValue(
                target,
                JsonValue("[1, \"two\"]"),
                "numbers"));

        Assert.Equal(PatchDiagnosticCode.InvalidPatchConfiguration, exception.Diagnostic.Code);
        Assert.Equal("numbers[1]", exception.Diagnostic.FieldPath);
        Assert.Contains("Int32", exception.Diagnostic.Expected);
    }

    private static JsonElement JsonValue(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }
}
