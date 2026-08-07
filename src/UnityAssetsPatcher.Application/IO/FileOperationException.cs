using UnityAssetsPatcher.Application.Failures;

namespace UnityAssetsPatcher.Application.IO;

public sealed class FileOperationException : ApplicationFailureException
{
    public override string Code { get; }

    public FileOperationException(
        string code,
        IReadOnlyDictionary<string, object?>? parameters = null,
        Exception? innerException = null)
        : base($"File operation failed with '{code}'.", parameters, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Code = code;
    }
}
