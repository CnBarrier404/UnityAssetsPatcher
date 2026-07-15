namespace UnityAssetsPatcher.TUI;

// TODO(tui-refactor): Remove this legacy page execution contract after every page runs as Terminal.Gui content.
public interface ITerminalPage
{
    public string Title { get; }

    public string Description { get; }

    public TerminalPageResult Run();
}
