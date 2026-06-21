using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace UnityAssetsPatcher.TUI.Localization;

internal static class JsonLocalizationProvider
{
    private static readonly Lazy<FrozenDictionary<string, string>> Strings = new(Load);

    public static string GetString(string key)
    {
        return Strings.Value.GetValueOrDefault(key, key);
    }

    private static FrozenDictionary<string, string> Load()
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        Assembly assembly = typeof(JsonLocalizationProvider).Assembly;

        LoadResource(assembly, "en-US", dict);

        CultureInfo current = CultureInfo.CurrentUICulture;

        while (!Equals(current, CultureInfo.InvariantCulture))
        {
            if (LoadResource(assembly, current.Name, dict))
            {
                break;
            }

            current = current.Parent;
        }

        return dict.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static bool LoadResource(Assembly assembly, string culture, Dictionary<string, string> target)
    {
        string resourceName = $"UnityAssetsPatcher.TUI.Localization.JSON.{culture}.json";

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(stream);

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            target[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return true;
    }
}
