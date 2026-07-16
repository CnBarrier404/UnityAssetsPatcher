using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.Application.Updates;

public sealed partial class GitHubUpdateChecker : IUpdateChecker
{
    private static readonly Regex Sha256Pattern = MyRegex();

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();

    private readonly HttpClient _httpClient;
    private readonly AppInfo _appInfo;

    public const string UpdateManifestUrl =
        "https://github.com/CnBarrier404/UnityAssetsPatcher/releases/latest/download/update.json";

    public const int MaximumManifestSize = 64 * 1024;

    public GitHubUpdateChecker(HttpClient httpClient, AppInfo appInfo)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(appInfo);

        _httpClient = httpClient;
        _appInfo = appInfo;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!SemanticVersion.TryParse(_appInfo.DisplayVersion, out SemanticVersion currentVersion))
        {
            return new UpdateCheckFailed();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UpdateManifestUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd("UnityAssetsPatcher");

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > MaximumManifestSize)
            {
                return new UpdateCheckFailed();
            }

            await using Stream content =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            using var manifestBuffer = new MemoryStream();

            byte[] buffer = new byte[8192];

            while (true)
            {
                int bytesRead = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                if (manifestBuffer.Length + bytesRead > MaximumManifestSize)
                {
                    return new UpdateCheckFailed();
                }

                manifestBuffer.Write(buffer, 0, bytesRead);
            }

            manifestBuffer.Position = 0;
            using JsonDocument document = await JsonDocument.ParseAsync(
                manifestBuffer,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out JsonElement schemaElement) ||
                !schemaElement.TryGetInt32(out int schemaVersion) ||
                schemaVersion != 1 ||
                !TryReadString(root, "version", out string? version) ||
                !TryReadHttpsUri(root, "releaseUrl", out Uri? releaseUrl) ||
                !TryReadHttpsUri(root, "downloadUrl", out Uri? downloadUrl) ||
                !TryReadString(root, "sha256", out string? sha256) ||
                !Sha256Pattern.IsMatch(sha256!) ||
                !SemanticVersion.TryParse(version, out SemanticVersion latestVersion))
            {
                return new UpdateCheckFailed();
            }

            if (latestVersion.CompareTo(currentVersion) <= 0)
            {
                return new UpToDate();
            }

            return new UpdateAvailable(new AvailableUpdate(
                version!,
                releaseUrl!,
                downloadUrl!,
                sha256!.ToLowerInvariant()));
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckFailed();
        }
        catch (OperationCanceledException)
        {
            return new UpdateCheckFailed();
        }
        catch (IOException)
        {
            return new UpdateCheckFailed();
        }
        catch (JsonException)
        {
            return new UpdateCheckFailed();
        }
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

    private readonly record struct SemanticVersion(
        int Major,
        int Minor,
        int Patch,
        string[] Prerelease) : IComparable<SemanticVersion>
    {
        public static bool TryParse(string? value, out SemanticVersion version)
        {
            version = default;

            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('v'))
            {
                return false;
            }

            string versionText = value[1..];
            int metadataIndex = versionText.IndexOf('+', StringComparison.Ordinal);

            if (metadataIndex >= 0)
            {
                versionText = versionText[..metadataIndex];
            }

            string[] versionAndPrerelease = versionText.Split('-', 2);
            string[] coreParts = versionAndPrerelease[0].Split('.');

            if (coreParts.Length != 3 ||
                !TryParsePart(coreParts[0], out int major) ||
                !TryParsePart(coreParts[1], out int minor) ||
                !TryParsePart(coreParts[2], out int patch))
            {
                return false;
            }

            string[] prerelease = versionAndPrerelease.Length == 1
                ? []
                : versionAndPrerelease[1].Split('.');

            if (prerelease.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }

            version = new SemanticVersion(major, minor, patch, prerelease);

            return true;
        }

        public int CompareTo(SemanticVersion other)
        {
            int coreComparison = Major.CompareTo(other.Major);
            coreComparison = coreComparison != 0 ? coreComparison : Minor.CompareTo(other.Minor);
            coreComparison = coreComparison != 0 ? coreComparison : Patch.CompareTo(other.Patch);

            if (coreComparison != 0)
            {
                return coreComparison;
            }

            if (Prerelease.Length == 0 || other.Prerelease.Length == 0)
            {
                return other.Prerelease.Length.CompareTo(Prerelease.Length);
            }

            for (int index = 0; index < Math.Min(Prerelease.Length, other.Prerelease.Length); index++)
            {
                int identifierComparison = ComparePrereleaseIdentifier(
                    Prerelease[index],
                    other.Prerelease[index]);

                if (identifierComparison != 0)
                {
                    return identifierComparison;
                }
            }

            return Prerelease.Length.CompareTo(other.Prerelease.Length);
        }

        private static bool TryParsePart(string value, out int part)
        {
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out part);
        }

        private static int ComparePrereleaseIdentifier(string left, string right)
        {
            bool leftIsNumber = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out int leftNumber);
            bool rightIsNumber =
                int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out int rightNumber);

            if (leftIsNumber && rightIsNumber)
            {
                return leftNumber.CompareTo(rightNumber);
            }

            if (leftIsNumber != rightIsNumber)
            {
                return leftIsNumber ? -1 : 1;
            }

            return string.Compare(left, right, StringComparison.Ordinal);
        }
    }
}
