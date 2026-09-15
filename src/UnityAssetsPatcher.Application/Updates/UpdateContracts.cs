namespace UnityAssetsPatcher.Application.Updates;

public sealed record UpdateInfo(string Version, Uri ReleaseUrl, Uri DownloadUrl, string Sha256);

public interface IUpdateChecker
{
    /// <summary>Checks the remote manifest for a release newer than the current version.</summary>
    /// <exception cref="HttpRequestException">Retrieving the manifest failed.</exception>
    /// <exception cref="IOException">Reading the manifest failed.</exception>
    /// <exception cref="System.Text.Json.JsonException">The manifest is not valid JSON.</exception>
    /// <exception cref="InvalidDataException">
    /// The manifest exceeds the size limit or contains invalid fields or an invalid version.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The check was canceled, or the HTTP request timed out. HTTP timeouts carry a
    /// <see cref="TimeoutException"/> as the direct <see cref="Exception.InnerException"/>,
    /// following the <see href="https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.sendasync?view=net-10.0">HttpClient contract</see>.
    /// Other cancellation exceptions must propagate to the lifetime owner unchanged.
    /// </exception>
    public Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
