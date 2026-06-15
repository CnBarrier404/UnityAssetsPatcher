namespace UnityAssetsPatcher.Core.Exception;

public class UnityAssetsPatcherException : System.Exception
{
    public UnityAssetsPatcherException(string message)
        : base(message) { }

    public UnityAssetsPatcherException(string message, System.Exception innerException)
        : base(message, innerException) { }
}
