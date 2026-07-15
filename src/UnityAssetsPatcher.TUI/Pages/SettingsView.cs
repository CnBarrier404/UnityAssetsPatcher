using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class SettingsView : View, ITerminalContentView
{
    public string ShortcutHint => LocalizedStrings.SettingsPage_ShortcutHint;

    private readonly TerminalSettings _settings;
    private readonly Button _verboseOutput;

    public SettingsView(TerminalSettings settings, Action returnToMainMenu)
    {
        _settings = settings;

        KeyDown += (_, key) =>
        {
            if (key != Key.Esc)
            {
                return;
            }

            key.Handled = true;
            returnToMainMenu();
        };

        var heading = new Label
        {
            Text = LocalizedStrings.MainMenu_Settings_Title,
            X = 0,
            Y = 0,
        };
        heading.SetScheme(TerminalGUITheme.Title);

        var description = new Label
        {
            Text = LocalizedStrings.MainMenu_Settings_Description,
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
        };
        description.SetScheme(TerminalGUITheme.Muted);

        _verboseOutput = new Button
        {
            X = 0,
            Y = 3,
            Width = 30,
            NoDecorations = true,
            NoPadding = true,
            ShadowStyle = ShadowStyles.None,
            TextAlignment = Alignment.Start,
        };
        _verboseOutput.SetScheme(CreateToggleScheme());
        _verboseOutput.Accepted += (_, _) => ToggleVerboseOutput();
        UpdateToggleText();

        var verboseDescription = new Label
        {
            Text = LocalizedStrings.SettingsPage_VerboseLoggingDescription,
            X = 36,
            Y = 3,
            Width = Dim.Fill(),
        };
        verboseDescription.SetScheme(TerminalGUITheme.Muted);

        _verboseOutput.HasFocusChanged += (_, _) =>
        {
            UpdateToggleText();
            verboseDescription.SetScheme(
                _verboseOutput.HasFocus ? TerminalGUITheme.Selected : TerminalGUITheme.Muted);
        };

        Add(heading, description, _verboseOutput, verboseDescription);
    }

    private void ToggleVerboseOutput()
    {
        _settings.VerboseOutput = !_settings.VerboseOutput;
        UpdateToggleText();
    }

    private void UpdateToggleText()
    {
        string indicator = _verboseOutput.HasFocus ? ">" : " ";
        string checkbox = _settings.VerboseOutput ? "[*]" : "[ ]";
        _verboseOutput.Text = $"{indicator} {checkbox} {LocalizedStrings.SettingsPage_VerboseLoggingName}";
    }

    private static Terminal.Gui.Drawing.Scheme CreateToggleScheme()
    {
        Attribute normal = TerminalGUITheme.Base.Normal;
        Attribute selected = TerminalGUITheme.Selected.Normal;

        return new Terminal.Gui.Drawing.Scheme
        {
            Normal = normal,
            Focus = selected,
            HotNormal = normal,
            HotFocus = selected,
            Active = selected,
        };
    }
}
