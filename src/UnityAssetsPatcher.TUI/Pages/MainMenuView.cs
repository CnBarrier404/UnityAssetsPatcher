using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class MainMenuView : View
{
    public event EventHandler<TerminalMenuItem>? ItemSelected;

    public MainMenuView(IReadOnlyList<TerminalMenuItem> items, AvailableUpdate? availableUpdate)
    {
        var heading = new Label { Text = LocalizedStrings.MainMenu_Title, X = 0, Y = 0 };
        heading.SetScheme(TerminalGUITheme.Title);
        Add(heading);

        int row = 2;

        if (availableUpdate is not null)
        {
            AddUpdate(availableUpdate, row);
            row += 3;
        }

        foreach (TerminalMenuItem item in items)
        {
            AddChoice(item, row);
            row += 2;
        }
    }

    private void AddUpdate(AvailableUpdate update, int row)
    {
        var available = new Label
        {
            Text = string.Format(LocalizedStrings.Update_AvailableFormat, update.Version),
            X = 0,
            Y = row,
            Width = Dim.Fill(),
        };
        available.SetScheme(TerminalGUITheme.Preview);
        var download = new Label
        {
            Text = string.Format(LocalizedStrings.Update_DownloadFormat, update.ReleaseUrl),
            X = 0,
            Y = row + 1,
            Width = Dim.Fill(),
        };
        download.SetScheme(TerminalGUITheme.Muted);
        Add(available, download);
    }

    private void AddChoice(TerminalMenuItem item, int row)
    {
        string normalText = $"  {item.Title}";
        string focusedText = $"> {item.Title}";
        var button = new Button
        {
            Text = normalText,
            X = 0,
            Y = row,
            Width = 30,
            NoDecorations = true,
            NoPadding = true,
            ShadowStyle = ShadowStyles.None,
            TextAlignment = Alignment.Start,
        };
        button.SetScheme(CreateChoiceScheme());
        var description = new Label
        {
            Text = item.Description,
            X = 36,
            Y = row,
            Width = Dim.Fill(),
        };
        description.SetScheme(TerminalGUITheme.Muted);
        button.HasFocusChanged += (_, _) =>
        {
            button.Text = button.HasFocus ? focusedText : normalText;
            description.SetScheme(button.HasFocus ? TerminalGUITheme.Selected : TerminalGUITheme.Muted);
        };
        button.Accepted += (_, _) => ItemSelected?.Invoke(this, item);
        Add(button, description);
    }

    private static Terminal.Gui.Drawing.Scheme CreateChoiceScheme()
    {
        var normal = TerminalGUITheme.Base.Normal;
        var selected = TerminalGUITheme.Selected.Normal;

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

public sealed record TerminalMenuItem(string Title, string Description, Func<Action, View> CreateView);
