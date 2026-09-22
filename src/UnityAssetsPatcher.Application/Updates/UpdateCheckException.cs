namespace UnityAssetsPatcher.Application.Updates;

public sealed class UpdateCheckException : Exception
{
    public UpdateCheckException(Exception innerException) : base("The update check failed.", innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
    }
}
