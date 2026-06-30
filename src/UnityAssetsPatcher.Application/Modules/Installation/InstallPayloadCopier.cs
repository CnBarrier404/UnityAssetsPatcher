using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallPayloadCopier
{
    public IReadOnlyList<InstallChange> Copy(
        ModPackage package,
        IReadOnlyList<InstallPayloadFilePlan> files,
        StepTimer timings)
    {
        return timings.Measure("copy-files", () =>
        {
            if (files.Count == 0)
            {
                return [];
            }

            var results = new List<InstallChange>();

            try
            {
                foreach (InstallPayloadFilePlan file in files)
                {
                    try
                    {
                        package.CopyPayloadFile(file.Source, file.DestinationPath);
                    }
                    catch (IOException ex) when (File.Exists(file.DestinationPath))
                    {
                        throw new IOException(
                            $"Payload file was created by another process during installation: {file.DestinationPath}",
                            ex);
                    }

                    results.Add(new InstallChange(InstallChangeKind.Payload, file.Source, file.DestinationPath));
                }

                return results.ToArray();
            }
            catch (Exception ex) when (results.Count > 0)
            {
                try
                {
                    Rollback(results);
                }
                catch (Exception rollbackException)
                {
                    throw new InvalidOperationException(
                        "Payload copy failed and rollback also failed.",
                        new AggregateException(ex, rollbackException));
                }

                throw;
            }
        });
    }

    public static void Rollback(IReadOnlyList<InstallChange> copiedFiles)
    {
        foreach (InstallChange file in copiedFiles.Reverse())
        {
            if (file.Kind != InstallChangeKind.Payload)
            {
                continue;
            }

            if (File.Exists(file.Path))
            {
                File.Delete(file.Path);
            }
        }
    }
}
