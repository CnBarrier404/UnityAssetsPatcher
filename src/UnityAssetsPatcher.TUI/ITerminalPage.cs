namespace UnityAssetsPatcher.TUI;

public interface ITerminalPage
{
    public string Title { get; }

    public string Description { get; }

    public TerminalPageResult Run();
}
