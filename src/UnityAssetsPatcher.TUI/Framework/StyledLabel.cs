using Terminal.Gui.Views;

namespace UnityAssetsPatcher.TUI.Framework;

public enum TextRole
{
    Base,
    Muted,
    Selected,
    Title,
    Label,
    SectionHeader,
    Preview,
    Error,
    Success,
}

public sealed class StyledLabel : Label
{
    public StyledLabel(string text = "", TextRole role = TextRole.Base)
    {
        Text = text;

        SetRole(role);
    }

    public void SetRole(TextRole role)
    {
        SetScheme(TerminalTheme.GetTextScheme(role));
    }
}
