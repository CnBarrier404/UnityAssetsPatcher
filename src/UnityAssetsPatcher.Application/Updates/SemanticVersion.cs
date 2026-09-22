using System.Globalization;

namespace UnityAssetsPatcher.Application.Updates;

internal readonly record struct SemanticVersion(int Major, int Minor, int Patch)
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

        string[] coreParts = versionText.Split('.');

        if (coreParts.Length != 3 ||
            !TryParsePart(coreParts[0], out int major) ||
            !TryParsePart(coreParts[1], out int minor) ||
            !TryParsePart(coreParts[2], out int patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch);

        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        int comparison = Major.CompareTo(other.Major);
        comparison = comparison != 0 ? comparison : Minor.CompareTo(other.Minor);
        return comparison != 0 ? comparison : Patch.CompareTo(other.Patch);
    }

    private static bool TryParsePart(string value, out int part)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out part);
    }
}
