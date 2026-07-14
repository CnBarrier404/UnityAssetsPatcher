using System.CommandLine;

namespace UnityAssetsPatcher.CLI;

public interface ICLICommand
{
    public Command Command { get; }
}
