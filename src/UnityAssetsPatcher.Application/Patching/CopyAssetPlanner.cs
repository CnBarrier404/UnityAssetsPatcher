using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class CopyAssetPlanner
{
    private readonly AssetQueryService _assetQueryService;

    public CopyAssetPlanner(AssetQueryService assetQueryService)
    {
        _assetQueryService = assetQueryService;
    }

    public CopyAssetPlanningOutput Plan(string assetsFilePath, IReadOnlyList<ModPatch> targets)
    {
        AssetQueryContext context = _assetQueryService.CreateContext(assetsFilePath);
        var copies = new List<AssetCopy>();
        var previews = new List<PatchPreviewAssetResult>();
        var targetPathIds = new HashSet<long>();

        foreach (ModPatch patch in targets.Where(target => target.CopyAsset is not null))
        {
            ModCopyAsset copyFrom = patch.CopyAsset!;
            AssetQueryMatch target = FindUniqueMatch(context, patch, "target");
            var sourcePatch = new ModPatch(
                patch.AssetsFileName,
                copyFrom.AssetTypeName,
                copyFrom.Match,
                [],
                [],
                null,
                null,
                null);
            AssetQueryMatch source = FindUniqueMatch(context, sourcePatch, "source");

            if (!string.Equals(target.Asset.TypeName, source.Asset.TypeName, StringComparison.OrdinalIgnoreCase))
            {
                throw new PatchPlanningException(
                    PatchDiagnosticCode.InvalidPatchConfiguration,
                    $"Copy asset source type '{source.Asset.TypeName}' does not match target type '{target.Asset.TypeName}'.");
            }

            if (source.Asset.PathId == target.Asset.PathId)
            {
                throw new PatchPlanningException(
                    PatchDiagnosticCode.InvalidPatchConfiguration,
                    "Copy asset source and target cannot be the same asset.");
            }

            if (!targetPathIds.Add(target.Asset.PathId))
            {
                throw new PatchPlanningException(
                    PatchDiagnosticCode.InvalidPatchConfiguration,
                    $"Copy asset target Path ID {target.Asset.PathId} is declared more than once.");
            }

            copies.Add(new AssetCopy(source.Asset.PathId, target.Asset.PathId));
            previews.Add(new PatchPreviewAssetResult(
                target.Asset,
                [
                    new PatchPreviewOperationResult(
                        "$copyAsset",
                        $"Path ID {target.Asset.PathId}",
                        $"Path ID {source.Asset.PathId}",
                        $"copy Path ID {source.Asset.PathId} and preserve m_Name",
                        true)
                ]));
        }

        if (copies.Any(copy => targetPathIds.Contains(copy.SourcePathId)))
        {
            throw new PatchPlanningException(
                PatchDiagnosticCode.InvalidPatchConfiguration,
                "Chained or cyclic 'copyAsset' operations are not supported.");
        }

        return new CopyAssetPlanningOutput(copies, new PatchPreviewResult(previews));
    }

    private static AssetQueryMatch FindUniqueMatch(
        AssetQueryContext context,
        ModPatch patch,
        string role)
    {
        AssetQueryMatch[] matches = [.. AssetQueryService.FindMatches(context, patch).Take(2)];

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new PatchPlanningException(
                PatchDiagnosticCode.InvalidPatchConfiguration,
                $"Copy asset {role} did not match an asset."),
            _ => throw new PatchPlanningException(
                PatchDiagnosticCode.InvalidPatchConfiguration,
                $"Copy asset {role} matched multiple assets.")
        };
    }
}

public sealed record CopyAssetPlanningOutput(IReadOnlyList<AssetCopy> Copies, PatchPreviewResult Preview);
