namespace UnityAssetsPatcher.Application.Packages;

public readonly record struct PackageEntryId(int Value);

public sealed record PackageEntryInfo(PackageEntryId Id, string Path, long Length, bool IsDirectory);

public interface IModPackageArchiveFactory
{
    public IModPackageArchive OpenRead(string packagePath);
}

public interface IModPackageArchive : IDisposable
{
    public string PackagePath { get; }

    public IReadOnlyList<PackageEntryInfo> Entries { get; }

    public Stream OpenEntry(PackageEntryId entryId);
}
