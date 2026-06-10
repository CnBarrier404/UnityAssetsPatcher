namespace UnityAssetsPatcher.Tui;

public interface ITerminalPage
{
    string Title { get; }

    string Description { get; }

    TerminalPageResult Run();
}
