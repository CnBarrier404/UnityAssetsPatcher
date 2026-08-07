using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Manifests;

internal sealed class ManifestServiceTestHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public IModManifestService Service { get; }

    private ManifestServiceTestHost(ServiceProvider provider)
    {
        _provider = provider;
        Service = provider.GetRequiredService<IModManifestService>();
    }

    public static ManifestServiceTestHost FromText(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return FromBytes(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public static ManifestServiceTestHost FromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return Create(_ => new MemoryStream(bytes, writable: false));
    }

    public static ManifestServiceTestHost Create(
        Func<string, Stream> openRead,
        Func<string, IModPackageArchive>? openArchive = null)
    {
        ArgumentNullException.ThrowIfNull(openRead);

        openArchive ??= _ => throw new InvalidOperationException("The archive factory should not be called.");

        var services = new ServiceCollection();
        services.AddSingleton<IFileSystemOperations>(_ => new StubFileSystemOperations(openRead));
        services.AddSingleton<IModPackageArchiveFactory>(_ => new StubModPackageArchiveFactory(openArchive));
        services.AddUnityAssetsPatcherApplication();

        ServiceProvider provider = services.BuildServiceProvider();

        return new ManifestServiceTestHost(provider);
    }

    public ModManifest Read(string sourcePath = "manifest.json")
    {
        return Service.ReadManifestAsync(sourcePath, TestContext.Current.CancellationToken)
            .GetAwaiter()
            .GetResult();
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    private sealed class StubFileSystemOperations : IFileSystemOperations
    {
        private readonly Func<string, Stream> _openRead;

        public StubFileSystemOperations(Func<string, Stream> openRead)
        {
            _openRead = openRead;
        }

        public Stream OpenRead(string path)
        {
            return _openRead(path);
        }

        public FileIntegrity ComputeFileIntegrity(string path)
        {
            throw new NotSupportedException();
        }

        public FileAttributes GetAttributes(string path)
        {
            throw new NotSupportedException();
        }

        public void WriteFileAtomically(string destinationPath, FileDestinationMode mode, Action<Stream> writer)
        {
            throw new NotSupportedException();
        }

        public void CopyFileAtomically(string sourcePath, string destinationPath, FileDestinationMode mode)
        {
            throw new NotSupportedException();
        }

        public void DeleteFile(string path)
        {
            throw new NotSupportedException();
        }

        public void EnsureDirectory(string path)
        {
            throw new NotSupportedException();
        }

        public void MoveDirectory(string sourcePath, string destinationPath)
        {
            throw new NotSupportedException();
        }

        public void DeleteDirectoryTree(string path)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubModPackageArchiveFactory : IModPackageArchiveFactory
    {
        private readonly Func<string, IModPackageArchive> _openRead;

        public StubModPackageArchiveFactory(Func<string, IModPackageArchive> openRead)
        {
            _openRead = openRead;
        }

        public IModPackageArchive OpenRead(string packagePath)
        {
            return _openRead(packagePath);
        }
    }
}
