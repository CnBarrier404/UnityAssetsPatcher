using System.Reflection;

namespace UnityAssetsPatcher;

internal static class BuildInfo
{
    public static string DisplayVersion { get; } = GetDisplayVersion();

    private static string GetDisplayVersion()
    {
        string? version = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(version) || !version.StartsWith('v'))
        {
            return "dev";
        }

        int metadataIndex = version.IndexOf('+', StringComparison.Ordinal);

        return metadataIndex < 0 ? version : version[..metadataIndex];
    }
}
