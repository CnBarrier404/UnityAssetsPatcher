using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace UnityAssetsPatcher.TUI.Framework;

public enum ActionKind
{
    Default,
    Primary,
    Secondary,
    Dangerous,
}

public sealed class ActionButton : Button
{
    private readonly string _text;

    public ActionButton(string text, ActionKind kind = ActionKind.Default)
    {
        ArgumentNullException.ThrowIfNull(text);

        _text = text;
        NoDecorations = true;
        NoPadding = true;
        ShadowStyle = ShadowStyles.None;
        TextAlignment = Alignment.Start;

        SetScheme(kind switch
        {
            ActionKind.Default => TerminalTheme.Interactive,
            ActionKind.Primary => TerminalTheme.PrimaryAction,
            ActionKind.Secondary => TerminalTheme.SecondaryAction,
            ActionKind.Dangerous => TerminalTheme.DangerousAction,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        });

        HasFocusChanged += (_, _) => UpdateText();

        UpdateText();
    }

    private void UpdateText()
    {
        Text = $"{(HasFocus ? ">" : " ")} {_text}";
    }
}
