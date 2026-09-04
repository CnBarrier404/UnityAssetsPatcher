using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Shell;

public sealed class TerminalPageHeaderView : View
{
    private readonly StyledLabel _title;
    private readonly StyledLabel _description;

    public TerminalPageHeaderView()
    {
        X = 0;
        Width = Dim.Fill();
        Height = 0;
        Visible = false;

        _title = new StyledLabel(role: TextRole.Title)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };

        _description = new StyledLabel(role: TextRole.Muted)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill()
        };

        Add(_title, _description);
    }

    internal void SetHeader(string title, string? description)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);

        bool hasDescription = !string.IsNullOrEmpty(description);

        _title.Text = title;
        _description.Text = description ?? string.Empty;
        _description.Visible = hasDescription;
        Height = hasDescription ? 3 : 2;
        Visible = true;
    }

    internal void ClearHeader()
    {
        _title.Text = string.Empty;
        _description.Text = string.Empty;
        _description.Visible = false;
        Height = 0;
        Visible = false;
    }
}
