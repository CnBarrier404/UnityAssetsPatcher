using System.Reflection;
using System.Text.Json;
using UnityAssetsPatcher.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsFieldWriterTests
{
    /// <summary>
    /// Verifies that float values outside the single range are rejected instead of being written as infinity.
    /// </summary>
    [Fact]
    public void WriteJsonValue_WhenFloatValueExceedsSingleRange_Throws()
    {
        object field = CreateFloatField();
        using JsonDocument document = JsonDocument.Parse("1e100");

        Assert.Throws<InvalidOperationException>(() => WriteJsonValue(field, document.RootElement));
    }

    private static object CreateFloatField()
    {
        Type fieldType = GetWriteJsonValueMethod().GetParameters()[0].ParameterType;
        Type templateFieldType = fieldType.GetProperty("TemplateField")?.PropertyType
                                 ?? throw new InvalidOperationException("TemplateField property was not found.");
        Type valueType = fieldType.GetProperty("Value")?.PropertyType
                         ?? throw new InvalidOperationException("Value property was not found.");
        Type assetValueType = templateFieldType.GetProperty("ValueType")?.PropertyType
                              ?? throw new InvalidOperationException("ValueType property was not found.");
        Type fieldListType = typeof(List<>).MakeGenericType(fieldType);

        object templateField = Activator.CreateInstance(templateFieldType)
                               ?? throw new InvalidOperationException("Template field could not be created.");
        templateFieldType.GetProperty("Name")?.SetValue(templateField, "field of view");
        templateFieldType.GetProperty("Type")?.SetValue(templateField, "float");
        templateFieldType.GetProperty("ValueType")?.SetValue(templateField, Enum.Parse(assetValueType, "Float"));
        templateFieldType.GetProperty("HasValue")?.SetValue(templateField, true);

        object value = Activator.CreateInstance(valueType, 0f)
                       ?? throw new InvalidOperationException("Field value could not be created.");
        object children = Activator.CreateInstance(fieldListType)
                          ?? throw new InvalidOperationException("Field children list could not be created.");
        object field = Activator.CreateInstance(fieldType)
                       ?? throw new InvalidOperationException("Field could not be created.");

        fieldType.GetProperty("TemplateField")?.SetValue(field, templateField);
        fieldType.GetProperty("Value")?.SetValue(field, value);
        fieldType.GetProperty("Children")?.SetValue(field, children);

        return field;
    }

    private static void WriteJsonValue(object field, JsonElement value)
    {
        try
        {
            GetWriteJsonValueMethod().Invoke(null, [field, value]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static MethodInfo GetWriteJsonValueMethod()
    {
        Type writerType = typeof(AssetsFileWriter).Assembly.GetType("UnityAssetsPatcher.AssetsTools.AssetsFieldWriter")
                          ?? throw new InvalidOperationException("AssetsFieldWriter type was not found.");

        return writerType.GetMethod(
                   "WriteJsonValue",
                   BindingFlags.Public | BindingFlags.Static)
               ?? throw new InvalidOperationException("WriteJsonValue method was not found.");
    }
}
