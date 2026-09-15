using System.Reflection;

namespace UnityAssetsPatcher.Application;

public static class AppConfig
{
    public const string Name = "UnityAssetsPatcher";
    public static string DisplayVersion { get; } = GetVersion();

    public static string ApplicationDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name);

    public static string LogDirectory { get; } = Path.Combine(ApplicationDataDirectory, "logs");
    public static string RepositoryDirectory { get; } = Path.Combine(ApplicationDataDirectory, "backup");
    public static string TemporaryDirectory { get; } = Path.GetTempPath();

    private static string GetVersion()
    {
        string? version = Assembly.GetEntryAssembly()?
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
