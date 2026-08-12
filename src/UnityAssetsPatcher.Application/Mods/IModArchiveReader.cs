namespace UnityAssetsPatcher.Application.Mods;

public interface IModArchiveReader
{
    public Task<IModArchiveSession> OpenAsync(string archivePath, CancellationToken cancellationToken = default);
}

public interface IModArchiveSession : IDisposable
{
    public IReadOnlyList<IModArchiveEntry> Entries { get; }
}

public interface IModArchiveEntry
{
    public string FullName { get; }
    public string Name { get; }
    public long Length { get; }

    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}
