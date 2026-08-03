namespace UnityAssetsPatcher.Domain.Assets;

public abstract record AssetWriteValue;

public sealed record AssetScalarWriteValue : AssetWriteValue
{
    public AssetScalarValue Value { get; }

    public AssetScalarWriteValue(AssetScalarValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Value = value;
    }
}

public sealed record AssetScalarArrayWriteValue : AssetWriteValue
{
    public AssetScalarKind ElementKind { get; }
    public IReadOnlyList<AssetScalarValue> Values { get; }

    public AssetScalarArrayWriteValue(AssetScalarKind elementKind, IEnumerable<AssetScalarValue?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        ElementKind = elementKind;
        AssetScalarValue?[] nullableValues = [.. values];

        if (nullableValues.Any(value => value is null || value.Kind != elementKind))
        {
            throw new ArgumentException("Every array value must be non-null and match the declared element kind.",
                nameof(values));
        }

        AssetScalarValue[] valueArray = [.. nullableValues.Select(value => value!)];
        Values = Array.AsReadOnly(valueArray);
    }
}

public abstract record AssetMutation;

public sealed class AssetMutationPlan
{
    public IReadOnlyList<AssetMutation> Mutations { get; }

    public AssetMutationPlan(IEnumerable<AssetMutation?> mutations)
    {
        ArgumentNullException.ThrowIfNull(mutations);

        AssetMutation?[] nullableMutations = [.. mutations];

        if (nullableMutations.Any(mutation => mutation is null))
        {
            throw new ArgumentException("The mutation plan cannot contain null entries.", nameof(mutations));
        }

        AssetMutation[] mutationArray = [.. nullableMutations.Select(mutation => mutation!)];
        Mutations = Array.AsReadOnly(mutationArray);
    }
}

public sealed record PatchAssetFields : AssetMutation
{
    public AssetPathId Asset { get; }
    public IReadOnlyList<SetAssetField> Assignments { get; }

    public PatchAssetFields(AssetPathId asset, IEnumerable<SetAssetField?> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        Asset = asset;
        SetAssetField?[] nullableAssignments = [.. assignments];

        if (nullableAssignments.Length == 0 || nullableAssignments.Any(assignment => assignment is null))
        {
            throw new ArgumentException("A field patch must contain at least one non-null assignment.",
                nameof(assignments));
        }

        SetAssetField[] assignmentArray = [.. nullableAssignments.Select(assignment => assignment!)];
        Assignments = Array.AsReadOnly(assignmentArray);
    }
}

public sealed record SetAssetField
{
    public AssetFieldPath Path { get; }
    public AssetWriteValue Value { get; }

    public SetAssetField(AssetFieldPath path, AssetWriteValue value)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(value);

        Path = path;
        Value = value;
    }
}

public sealed record CopyAsset(AssetPathId Source, AssetPathId Target) : AssetMutation;

public sealed record AssetSource
{
    public string AssetsFilePath { get; }
    public AssetPathId Asset { get; }

    public AssetSource(string assetsFilePath, AssetPathId asset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);

        AssetsFilePath = assetsFilePath;
        Asset = asset;
    }
}

public sealed record ReplaceAsset : AssetMutation
{
    public AssetSource Source { get; }
    public AssetPathId Target { get; }

    public ReplaceAsset(AssetSource source, AssetPathId target)
    {
        ArgumentNullException.ThrowIfNull(source);

        Source = source;
        Target = target;
    }
}
