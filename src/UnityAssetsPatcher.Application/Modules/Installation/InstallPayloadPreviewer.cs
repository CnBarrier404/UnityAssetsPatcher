using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallPayloadPreviewer
{
    public IReadOnlyList<InstallChange> Preview(IReadOnlyList<InstallPayloadFilePlan> files)
    {
        return files
            .Select(file => new InstallChange(
                InstallChangeKind.Payload,
                file.Source,
                file.DestinationPath,
                WillCopy: !File.Exists(file.DestinationPath)))
            .ToArray();
    }
}
