using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class MainMenuView : View
{
    public event EventHandler<TerminalMenuItem>? ItemSelected;

    public MainMenuView(IReadOnlyList<TerminalMenuItem> items, AvailableUpdate? availableUpdate)
    {
        ArgumentNullException.ThrowIfNull(items);
        var heading = new StyledLabel(LocalizedStrings.MainMenu_Title, TextRole.Title) { X = 0, Y = 0 };
        Add(heading);

        int row = 2;

        if (availableUpdate is not null)
        {
            AddUpdate(availableUpdate, row);
            row += 3;
        }

        ActionButton? firstButton = null;
        foreach (TerminalMenuItem item in items)
        {
            ActionButton button = AddChoice(item, row);
            firstButton ??= button;
            row += 2;
        }

        Initialized += (_, _) => firstButton?.SetFocus();
    }

    private void AddUpdate(AvailableUpdate update, int row)
    {
        var available = new StyledLabel(
            string.Format(LocalizedStrings.Update_AvailableFormat, update.Version), TextRole.Preview)
        {
            X = 0,
            Y = row,
            Width = Dim.Fill(),
        };
        var download = new StyledLabel(
            string.Format(LocalizedStrings.Update_DownloadFormat, update.ReleaseUrl), TextRole.Muted)
        {
            X = 0,
            Y = row + 1,
            Width = Dim.Fill(),
        };
        Add(available, download);
    }

    private ActionButton AddChoice(TerminalMenuItem item, int row)
    {
        var choice = new ChoiceItem(item.Title, item.Description) { X = 0, Y = row };
        choice.Button.Accepted += (_, _) => ItemSelected?.Invoke(this, item);
        Add(choice);
        return choice.Button;
    }
}

public sealed record TerminalMenuItem(string Title, string Description, Func<Action, View> CreateView);
