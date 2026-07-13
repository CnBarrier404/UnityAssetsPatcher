using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.Application.Updates;

public sealed class GitHubUpdateChecker : IUpdateChecker
{
    public const string LatestReleaseUrl =
        "https://api.github.com/repos/CnBarrier404/UnityAssetsPatcher/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly AppInfo _appInfo;

    public GitHubUpdateChecker(HttpClient httpClient, AppInfo appInfo)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(appInfo);

        _httpClient = httpClient;
        _appInfo = appInfo;
    }

    public AvailableUpdate? CheckForUpdate()
    {
        if (!SemanticVersion.TryParse(_appInfo.DisplayVersion, out SemanticVersion currentVersion))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("UnityAssetsPatcher");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using HttpResponseMessage response = _httpClient.Send(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using Stream content = response.Content.ReadAsStream();
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("tag_name", out JsonElement tagElement) ||
                tagElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("html_url", out JsonElement urlElement) ||
                urlElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? tagName = tagElement.GetString();
            string? releaseUrl = urlElement.GetString();

            if (!SemanticVersion.TryParse(tagName, out SemanticVersion latestVersion) ||
                latestVersion.CompareTo(currentVersion) <= 0 ||
                !Uri.TryCreate(releaseUrl, UriKind.Absolute, out Uri? parsedReleaseUrl) ||
                parsedReleaseUrl.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            return new AvailableUpdate(tagName!, parsedReleaseUrl);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
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
