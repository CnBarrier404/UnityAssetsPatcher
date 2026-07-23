namespace UnityAssetsPatcher.Domain.Assets;

public sealed record AssetFieldPatch(long PathId, IReadOnlyList<FieldPatchOperation> Operations);
