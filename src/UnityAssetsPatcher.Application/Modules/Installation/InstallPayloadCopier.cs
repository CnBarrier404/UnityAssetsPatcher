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
        });
    }
}
