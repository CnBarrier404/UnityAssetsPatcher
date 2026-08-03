using System.Globalization;

namespace UnityAssetsPatcher.Infrastructure.Updates;

internal readonly record struct SemanticVersion(int Major, int Minor, int Patch, string[] Prerelease)
    : IComparable<SemanticVersion>
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
            int identifierComparison = ComparePrereleaseIdentifier(Prerelease[index], other.Prerelease[index]);

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
