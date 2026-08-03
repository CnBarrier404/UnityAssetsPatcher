using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Application.Installation;

public sealed class GameDirectoryResolver
{
    private readonly IGameInstallationLocator _installationLocator;
    private readonly TrustedPathResolver _pathResolver;

    public GameDirectoryResolver(
        IGameInstallationLocator installationLocator,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(installationLocator);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _installationLocator = installationLocator;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
    }

    public string? Resolve(string game)
    {
        if (string.IsNullOrWhiteSpace(game))
        {
            return null;
        }

        string[] matches = _installationLocator
            .FindGameDirectories(game)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Select(_pathResolver.ResolveExistingDirectory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
    }

    public string ResolveRequired(string? gameDirectory, string? manifestGame)
    {
        if (!string.IsNullOrWhiteSpace(gameDirectory))
        {
            string fullGameDirectory = Path.GetFullPath(gameDirectory);

            try
            {
                return _pathResolver.ResolveExistingDirectory(fullGameDirectory);
            }
            catch (DirectoryNotFoundException exception)
            {
                throw new DirectoryNotFoundException(
                    $"Game directory not found: {fullGameDirectory}",
                    exception);
            }
        }

        if (string.IsNullOrWhiteSpace(manifestGame))
        {
            throw new DirectoryNotFoundException(
                "Game directory was not provided and manifest does not contain a 'game' property.");
        }

        string? resolvedDirectory = Resolve(manifestGame);

        return resolvedDirectory ?? throw new DirectoryNotFoundException(
            $"Game directory could not be resolved for manifest game: {manifestGame}");
    }
}
