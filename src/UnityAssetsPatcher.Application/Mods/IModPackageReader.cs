namespace UnityAssetsPatcher.Application.Mods;

public interface IModPackageReader
{
    public Task<IModPackageSession> OpenAsync(string archivePath, CancellationToken cancellationToken = default);
}

public interface IModPackageSession : IDisposable
{
    public IReadOnlyList<IModPackageEntry> Entries { get; }
}

public interface IModPackageEntry
{
    public string FullName { get; }
    public string Name { get; }
    public long Length { get; }

    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}
