using System.Text.Json;
using UnityAssetsPatcher.Application.Updates;

namespace UnityAssetsPatcher.Infrastructure.Updates;

internal enum GitHubUpdateReadStatus
{
    Success,
    TooLarge,
    Invalid
}

internal sealed record GitHubUpdateReadResult(UpdateInfo? Release, GitHubUpdateReadStatus Status);

internal static class GitHubUpdateReader
{
    public static async Task<GitHubUpdateReadResult> ReadAsync(
        Stream contentStream,
        int maximumSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contentStream);

        using MemoryStream releaseBuffer = new();

        byte[] buffer = new byte[8192];

        while (true)
        {
            int bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            if (releaseBuffer.Length + bytesRead > maximumSize)
            {
                return new GitHubUpdateReadResult(null, GitHubUpdateReadStatus.TooLarge);
            }

            releaseBuffer.Write(buffer, 0, bytesRead);
        }

        releaseBuffer.Position = 0;

        using JsonDocument document = await JsonDocument.ParseAsync(
            releaseBuffer,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return TryReadRelease(document.RootElement, out UpdateInfo? release)
            ? new GitHubUpdateReadResult(release, GitHubUpdateReadStatus.Success)
            : new GitHubUpdateReadResult(null, GitHubUpdateReadStatus.Invalid);
    }

    private static bool TryReadRelease(JsonElement root, out UpdateInfo? release)
    {
        release = null;

        if (root.ValueKind != JsonValueKind.Object ||
            !TryReadString(root, "tag_name", out string? version) ||
            !TryReadHttpsUri(root, "html_url", out Uri? releaseUrl))
        {
            return false;
        }

        release = new UpdateInfo(
            version!,
            releaseUrl!);

        return true;
    }

    private static bool TryReadString(JsonElement root, string propertyName, out string? value)
    {
        value = null;

        if (!root.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();

        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadHttpsUri(JsonElement root, string propertyName, out Uri? uri)
    {
        uri = null;

        return TryReadString(root, propertyName, out string? value) &&
               Uri.TryCreate(value, UriKind.Absolute, out uri) &&
               uri.Scheme == Uri.UriSchemeHttps;
    }
}
