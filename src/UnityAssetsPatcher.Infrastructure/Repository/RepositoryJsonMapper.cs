using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal static class RepositoryJsonMapper
{
    public static RepositoryMetadata Map(RepositoryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new RepositoryMetadata(
            document.FormatVersion,
            Require(document.RepositoryId, "repository ID"));
    }

    public static RepositoryDocument Map(RepositoryMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new RepositoryDocument(metadata.FormatVersion, metadata.RepositoryId);
    }

    private static string Require(string? value, string description)
    {
        return value ?? throw Invalid($"{description} is missing");
    }

    private static InvalidDataException Invalid(string detail, Exception? innerException = null)
    {
        return new InvalidDataException($"Backup repository data is invalid: {detail}.", innerException);
    }
}
