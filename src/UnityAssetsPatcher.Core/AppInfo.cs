using System.Reflection;

namespace UnityAssetsPatcher.Core;

public sealed record AppInfo(string Name, string DisplayVersion)
{
    public static AppInfo Default { get; } = new("Unity Assets Patcher", "dev");

    public static AppInfo FromAssembly(string name, Assembly assembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(assembly);

        string? version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return FromVersion(name, version);
    }

    public static AppInfo FromVersion(string name, string? version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (string.IsNullOrWhiteSpace(version) || !version.StartsWith('v'))
        {
            return new AppInfo(name, "dev");
        }

        int metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        string displayVersion = metadataIndex < 0 ? version : version[..metadataIndex];

        return new AppInfo(name, displayVersion);
    }
}
