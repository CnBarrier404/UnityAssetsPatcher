using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class MainMenuView : View
{
    public event EventHandler<TerminalMenuItem>? ItemSelected;

    private readonly List<ChoiceItem> _choices = [];
    private bool _hasUpdate;

    public MainMenuView(IReadOnlyList<TerminalMenuItem> items, AvailableUpdate? availableUpdate)
    {
        ArgumentNullException.ThrowIfNull(items);
        int headingRow = availableUpdate is null ? 0 : 3;
        var heading = new StyledLabel(LocalizedStrings.MainMenu_Title, TextRole.Title)
        {
            X = 0,
            Y = headingRow,
        };
        Add(heading);

        int row = headingRow + 2;

        if (availableUpdate is not null)
        {
            AddUpdate(availableUpdate, 0);
            _hasUpdate = true;
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

    public void ShowAvailableUpdate(AvailableUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (_hasUpdate)
        {
            return;
        }

        AddUpdate(update, 0);
        _hasUpdate = true;

        StyledLabel heading = SubViews.OfType<StyledLabel>().Single(view =>
            string.Equals(view.Text?.ToString(), LocalizedStrings.MainMenu_Title, StringComparison.Ordinal));
        heading.Y = 3;

        for (int index = 0; index < _choices.Count; index++)
        {
            _choices[index].Y = 5 + (index * 2);
        }
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
        _choices.Add(choice);
        Add(choice);
        return choice.Button;
    }
}

public sealed record TerminalMenuItem(string Title, string Description, Func<Action, View> CreateView);
