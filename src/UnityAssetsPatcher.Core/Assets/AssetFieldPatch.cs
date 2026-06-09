namespace UnityAssetsPatcher.Core.Assets;

public sealed record AssetFieldPatch(long PathId, IReadOnlyList<FieldPatchOperation> Operations);
