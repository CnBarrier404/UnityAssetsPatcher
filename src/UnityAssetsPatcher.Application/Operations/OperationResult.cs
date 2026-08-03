namespace UnityAssetsPatcher.Application.Operations;

public abstract record OperationResult<T>;

public sealed record OperationSucceeded<T>(T Value) : OperationResult<T>;

public sealed record OperationFailed<T> : OperationResult<T>
{
    public OperationError Error { get; }

    public OperationFailed(OperationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        Error = error;
    }
}
