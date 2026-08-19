using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class FileRepositoryOperationLockProvider : IRepositoryOperationLockProvider
{
    private readonly FileRepositoryLayout _layout;

    public FileRepositoryOperationLockProvider(FileRepositoryLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        _layout = layout;
    }

    public IRepositoryOperationLock Acquire()
    {
        try
        {
            return new FileRepositoryOperationLock(
                new FileStream(_layout.LockPath, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.None,
                    Options = FileOptions.DeleteOnClose
                }),
                _layout.RepositoryDirectory);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                "Another install, uninstall, or recovery operation is running.",
                exception);
        }
    }

    private sealed class FileRepositoryOperationLock : IRepositoryOperationLock
    {
        private readonly FileStream _stream;
        private readonly string _repositoryDirectory;

        public FileRepositoryOperationLock(FileStream stream, string repositoryDirectory)
        {
            _stream = stream;
            _repositoryDirectory = repositoryDirectory;
        }

        public void EnsureHeldFor(string repositoryDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);

            if (_stream.SafeFileHandle.IsClosed)
            {
                throw new InvalidOperationException("The backup operation lock is no longer held.");
            }

            string normalizedRepositoryDirectory = TrustedPath.NormalizeAbsolutePath(repositoryDirectory);

            if (!TrustedPath.PathsEqual(_repositoryDirectory, normalizedRepositoryDirectory))
            {
                throw new InvalidOperationException("The backup operation lock belongs to another repository.");
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
