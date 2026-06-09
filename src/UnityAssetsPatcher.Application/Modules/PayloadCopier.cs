using System.IO.Compression;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class PayloadCopier
{
    public PayloadCopyResult Execute(PayloadPlan plan, WorkflowTiming timings)
    {
        return timings.MeasureCopyFiles(() =>
        {
            if (plan.Files.Count == 0)
            {
                return new PayloadCopyResult([]);
            }

            var results = new List<PayloadCopiedFile>();

            using ZipArchive archive = ZipFile.OpenRead(plan.PackagePath);

            foreach (PayloadFilePlan file in plan.Files)
            {
                ZipArchiveEntry entry = PackageArchive.FindFileEntry(archive, file.Source, plan.PackagePath);
                PackageArchive.CopyEntryToNewFile(entry, file.DestinationPath);
                results.Add(new PayloadCopiedFile(file.Source, file.DestinationPath));
            }

            return new PayloadCopyResult(results);
        });
    }
}

public sealed record PayloadCopyResult(IReadOnlyList<PayloadCopiedFile> Files);

public sealed record PayloadCopiedFile(string Source, string DestinationPath);
