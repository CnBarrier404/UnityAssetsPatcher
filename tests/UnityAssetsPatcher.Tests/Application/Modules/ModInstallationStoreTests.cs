using System.Text.RegularExpressions;
using UnityAssetsPatcher.Application.Modules;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Modules;

public sealed class ModInstallationStoreTests
{
    [Fact]
    public void CreateInstallDirectory_RemovesWhitespaceAndInvalidCharactersFromModNameAndVersion()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var store = new ModInstallationStore(
            backupDirectory,
            () => new DateTimeOffset(2026, 6, 18, 14, 30, 22, TimeSpan.Zero));

        try
        {
            string installDirectory = store.CreateInstallDirectory("Better: Audio / Pack", "v1 beta");

            Assert.Equal(
                Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-v1beta"),
                installDirectory);
            Assert.True(Directory.Exists(installDirectory));
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    [Fact]
    public void CreateInstallDirectory_WhenNameCollides_AppendsUniqueSuffix()
    {
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var store = new ModInstallationStore(
            backupDirectory,
            () => new DateTimeOffset(2026, 6, 18, 14, 30, 22, TimeSpan.Zero));

        try
        {
            string first = store.CreateInstallDirectory("Better Audio Pack", "1.0.0");
            string second = store.CreateInstallDirectory("Better Audio Pack", "1.0.0");

            Assert.Matches(
                Regex.Escape(Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0")) + "$",
                first);
            Assert.Matches(
                Regex.Escape(Path.Combine(backupDirectory, "20260618143022-BetterAudioPack-1.0.0.1")) + "$",
                second);
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }
}
