using Microsoft.Win32;
using System.Runtime.Versioning;
using System.Security;

namespace UnityAssetsPatcher.Infrastructure.Installation;

public sealed record SteamInstallationOptions(IReadOnlyList<string> RootDirectories)
{
    public static SteamInstallationOptions FromCurrentMachine()
    {
        var roots = new List<string>();

        AddProgramFilesRoot(roots);
        AddRegistryRoots(roots);
        AddDriveRoots(roots);

        string[] normalizedRoots = roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SteamInstallationOptions(normalizedRoots);
    }

    private static void AddProgramFilesRoot(List<string> roots)
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            roots.Add(Path.Combine(programFiles, "Steam"));
        }
    }

    private static void AddRegistryRoots(List<string> roots)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        AddRegistryValue(roots, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        AddRegistryValue(roots, Registry.LocalMachine, @"Software\Valve\Steam", "InstallPath");
        AddRegistryValue(roots, Registry.LocalMachine, @"Software\WOW6432Node\Valve\Steam", "InstallPath");
    }

    [SupportedOSPlatform("windows")]
    private static void AddRegistryValue(
        List<string> roots,
        RegistryKey hive,
        string keyPath,
        string valueName)
    {
        try
        {
            using RegistryKey? key = hive.OpenSubKey(keyPath);

            if (key?.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
            {
                roots.Add(value.Replace('/', Path.DirectorySeparatorChar));
            }
        }
        catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException)
        {
            // An inaccessible registry key must not prevent discovery through other sources.
        }
    }

    private static void AddDriveRoots(List<string> roots)
    {
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                roots.Add(Path.Combine(drive.RootDirectory.FullName, "Steam"));
                roots.Add(Path.Combine(drive.RootDirectory.FullName, "SteamLibrary"));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // An unavailable drive must not prevent discovery on other drives.
            }
        }
    }
}
