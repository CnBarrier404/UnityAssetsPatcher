using UnityAssetsPatcher.Application.Failures;

namespace UnityAssetsPatcher.Application.Manifests;

public sealed class ManifestException : ApplicationFailureException
{
    public override string Code { get; }

    public ManifestException(
        string code,
        IReadOnlyDictionary<string, object?>? parameters = null,
        Exception? innerException = null)
        : base($"Manifest operation failed with '{code}'.", parameters, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Code = code;
    }
}
