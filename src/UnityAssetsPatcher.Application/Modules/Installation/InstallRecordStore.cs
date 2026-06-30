namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallRecordStore
{
    private readonly ModInstallationStore _recordStore;

    public InstallRecordStore(string backupDirectory)
    {
        _recordStore = new ModInstallationStore(backupDirectory);
    }

    public InstallRecordPaths CreateInstall(ModPackage package)
    {
        string installDirectory = _recordStore.CreateInstallDirectory(
            package.Manifest.Name,
            package.Manifest.Version);

        return new InstallRecordPaths(
            installDirectory,
            Path.Combine(installDirectory, "assets"));
    }

    public void Save(InstallRecord record, InstallRecordPaths paths)
    {
        _recordStore.Save(record, paths.InstallDirectory);
    }
}

public sealed record InstallRecordPaths(string InstallDirectory, string AssetsBackupDirectory);
