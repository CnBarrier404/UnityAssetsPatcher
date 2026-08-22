using System.Text.Json;
using System.Text.RegularExpressions;
using UnityAssetsPatcher.Application.Updates;

namespace UnityAssetsPatcher.Infrastructure.Updates;

internal enum UpdateManifestReadStatus
{
    Success,
    TooLarge,
    Invalid
}

internal sealed record UpdateManifestReadResult(
    UpdateInfo? Manifest,
    UpdateManifestReadStatus Status);

internal static partial class UpdateManifestReader
{
    private static readonly Regex Sha256Pattern = CreateSha256Pattern();

    public static async Task<UpdateManifestReadResult> ReadAsync(
        Stream contentStream,
        int maximumSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contentStream);

        using MemoryStream manifestBuffer = new();

        byte[] buffer = new byte[8192];

        while (true)
        {
            int bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            if (manifestBuffer.Length + bytesRead > maximumSize)
            {
                return new UpdateManifestReadResult(null, UpdateManifestReadStatus.TooLarge);
            }

            manifestBuffer.Write(buffer, 0, bytesRead);
        }

        manifestBuffer.Position = 0;

        using JsonDocument document = await JsonDocument.ParseAsync(
            manifestBuffer,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return TryReadManifest(document.RootElement, out UpdateInfo? manifest)
            ? new UpdateManifestReadResult(manifest, UpdateManifestReadStatus.Success)
            : new UpdateManifestReadResult(null, UpdateManifestReadStatus.Invalid);
    }

    private static bool TryReadManifest(JsonElement root, out UpdateInfo? manifest)
    {
        manifest = null;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out JsonElement schemaElement) ||
            !schemaElement.TryGetInt32(out int schemaVersion) ||
            schemaVersion != 1 ||
            !TryReadString(root, "version", out string? version) ||
            !TryReadHttpsUri(root, "releaseUrl", out Uri? releaseUrl) ||
            !TryReadHttpsUri(root, "downloadUrl", out Uri? downloadUrl) ||
            !TryReadString(root, "sha256", out string? sha256) ||
            !Sha256Pattern.IsMatch(sha256!))
        {
            return false;
        }

        manifest = new UpdateInfo(
            version!,
            releaseUrl!,
            downloadUrl!,
            sha256!.ToLowerInvariant());

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

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CreateSha256Pattern();
}
