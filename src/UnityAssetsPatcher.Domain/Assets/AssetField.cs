namespace UnityAssetsPatcher.Domain.Assets;

public abstract record AssetField
{
    public string Name { get; }
    public string TypeName { get; }
    public AssetFieldValue? Value => this is AssetScalarField scalar ? ToLegacyValue(scalar.Value) : null;
    public abstract IReadOnlyList<AssetField> Children { get; }

    protected AssetField(string name, string typeName)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(typeName);

        Name = name;
        TypeName = typeName;
    }

    private static AssetFieldValue ToLegacyValue(AssetScalarValue value)
    {
        return value switch
        {
            AssetScalarValue.Boolean boolean => new AssetFieldValue.Boolean(boolean.Value),
            AssetScalarValue.Int8 integer => new AssetFieldValue.Int64(integer.Value),
            AssetScalarValue.UInt8 integer => new AssetFieldValue.UInt64(integer.Value),
            AssetScalarValue.Int16 integer => new AssetFieldValue.Int64(integer.Value),
            AssetScalarValue.UInt16 integer => new AssetFieldValue.UInt64(integer.Value),
            AssetScalarValue.Int32 integer => new AssetFieldValue.Int64(integer.Value),
            AssetScalarValue.UInt32 integer => new AssetFieldValue.UInt64(integer.Value),
            AssetScalarValue.Int64 integer => new AssetFieldValue.Int64(integer.Value),
            AssetScalarValue.UInt64 integer => new AssetFieldValue.UInt64(integer.Value),
            AssetScalarValue.Float single => new AssetFieldValue.Float(single.Value),
            AssetScalarValue.Double doubleValue => new AssetFieldValue.Double(doubleValue.Value),
            AssetScalarValue.String text => new AssetFieldValue.String(text.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported scalar value."),
        };
    }

    public AssetField? FindChild(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.Ordinal));
    }

    public IReadOnlyList<AssetField> FindChildren(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Children.Where(child => string.Equals(child.Name, name, StringComparison.Ordinal)).ToArray();
    }
}

public sealed record AssetScalarField : AssetField
{
    public new AssetScalarValue Value { get; }
    public override IReadOnlyList<AssetField> Children => [];

    public AssetScalarField(string name, string typeName, AssetScalarValue value)
        : base(name, typeName)
    {
        ArgumentNullException.ThrowIfNull(value);

        Value = value;
    }
}

public sealed record AssetArrayField : AssetField
{
    public AssetFieldSchema ElementSchema { get; }
    public IReadOnlyList<AssetField> Elements { get; }
    public override IReadOnlyList<AssetField> Children => Elements;

    public AssetArrayField(
        string name,
        string typeName,
        AssetFieldSchema elementSchema,
        IEnumerable<AssetField?> elements)
        : base(name, typeName)
    {
        ArgumentNullException.ThrowIfNull(elementSchema);
        ArgumentNullException.ThrowIfNull(elements);

        ElementSchema = elementSchema;
        AssetField?[] nullableElements = [.. elements];

        if (nullableElements.Any(element => element is null))
        {
            throw new ArgumentException("Array fields cannot contain null elements.", nameof(elements));
        }

        AssetField[] elementArray = [.. nullableElements.Select(element => element!)];
        Elements = Array.AsReadOnly(elementArray);
    }
}

public sealed record AssetObjectField : AssetField
{
    public override IReadOnlyList<AssetField> Children { get; }

    public AssetObjectField(string name, string typeName, IEnumerable<AssetField?> children) : base(name, typeName)
    {
        ArgumentNullException.ThrowIfNull(children);

        AssetField?[] nullableChildren = [.. children];

        if (nullableChildren.Any(child => child is null))
        {
            throw new ArgumentException("Object fields cannot contain null children.", nameof(children));
        }

        AssetField[] childArray = [.. nullableChildren.Select(child => child!)];
        Children = Array.AsReadOnly(childArray);
    }
}

public abstract record AssetFieldSchema
{
    public string TypeName { get; }

    protected AssetFieldSchema(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        TypeName = typeName;
    }
}

public sealed record AssetScalarFieldSchema : AssetFieldSchema
{
    public AssetScalarKind Kind { get; }

    public AssetScalarFieldSchema(string typeName, AssetScalarKind kind) : base(typeName)
    {
        Kind = kind;
    }
}

public sealed record AssetArrayFieldSchema : AssetFieldSchema
{
    public AssetFieldSchema Element { get; }

    public AssetArrayFieldSchema(string typeName, AssetFieldSchema element) : base(typeName)
    {
        ArgumentNullException.ThrowIfNull(element);

        Element = element;
    }
}

public sealed record AssetObjectFieldSchema : AssetFieldSchema
{
    public IReadOnlyList<AssetFieldSchema> Children => _children;

    private readonly IReadOnlyList<AssetFieldSchema> _children;

    public AssetObjectFieldSchema(string typeName, IEnumerable<AssetFieldSchema?> children) : base(typeName)
    {
        ArgumentNullException.ThrowIfNull(children);

        AssetFieldSchema?[] nullableChildren = [.. children];

        if (nullableChildren.Any(child => child is null))
        {
            throw new ArgumentException("Object schemas cannot contain null children.", nameof(children));
        }

        AssetFieldSchema[] childArray = [.. nullableChildren.Select(child => child!)];
        _children = Array.AsReadOnly(childArray);
    }
}
