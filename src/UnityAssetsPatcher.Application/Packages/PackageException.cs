using UnityAssetsPatcher.Application.Failures;

namespace UnityAssetsPatcher.Application.Packages;

public sealed class PackageException : ApplicationFailureException
{
    private readonly string _code;

    public override string Code => _code;

    public PackageException(
        string code,
        IReadOnlyDictionary<string, object?>? parameters = null,
        Exception? innerException = null)
        : base($"Package operation failed with '{code}'.", parameters, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        _code = code;
    }
}
