namespace UnityAssetsPatcher.Application.Assets;

public sealed record AssetFieldPatch(long PathId, IReadOnlyList<FieldPatchOperation> Operations);
