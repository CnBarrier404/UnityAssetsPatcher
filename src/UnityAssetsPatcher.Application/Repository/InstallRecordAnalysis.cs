namespace UnityAssetsPatcher.Application.Repository;

public static class InstallSequenceAllocator
{
    public static long Allocate(
        IEnumerable<LayerRecord> layers,
        string gameInstanceFingerprint,
        string repositoryId)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameInstanceFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        LayerRecord[] snapshot = [.. layers];

        foreach (LayerRecord layer in snapshot)
        {
            if (!string.Equals(layer.RepositoryId, repositoryId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Layer record does not belong to this backup repository.");
            }
        }

        long maximum = snapshot
            .Where(layer => string.Equals(
                layer.GameInstanceFingerprint,
                gameInstanceFingerprint,
                StringComparison.Ordinal))
            .Select(layer => layer.InstallSequence)
            .DefaultIfEmpty(0)
            .Max();

        return checked(maximum + 1);
    }
}
