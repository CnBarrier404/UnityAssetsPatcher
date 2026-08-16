using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed record TerminalMenuItem(string Title, string Description, Func<Action, View> CreateView);

public sealed record TerminalUpdateNotice(string AvailableText, string DownloadText);

public sealed class MainMenuView : View
{
    public event EventHandler<TerminalMenuItem>? ItemSelected;

    private readonly List<ChoiceItemList> _choices = [];
    private readonly View _updateArea;
    private bool _hasUpdate;

    public MainMenuView(
        string title,
        IReadOnlyList<TerminalMenuItem> items,
        TerminalUpdateNotice? updateNotice = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(items);

        _updateArea = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Auto()
        };

        var heading = new StyledLabel(title, TextRole.Title)
        {
            X = 0,
            Y = Pos.Bottom(_updateArea)
        };

        Add(_updateArea, heading);

        if (updateNotice is not null)
        {
            AddUpdate(updateNotice);
            _hasUpdate = true;
        }

        View previous = heading;
        ActionButton? firstButton = null;

        foreach (TerminalMenuItem item in items)
        {
            var choice = new ChoiceItemList(item.Title, item.Description)
            {
                X = 0,
                Y = Pos.Bottom(previous) + 1
            };

            choice.Button.Accepted += (_, _) => ItemSelected?.Invoke(this, item);
            _choices.Add(choice);

            Add(choice);

            firstButton ??= choice.Button;
            previous = choice;
        }

        ChoiceItemList.AlignDescriptions(_choices);

        Initialized += (_, _) => firstButton?.SetFocus();
    }

    public void ShowAvailableUpdate(TerminalUpdateNotice updateNotice)
    {
        ArgumentNullException.ThrowIfNull(updateNotice);

        if (_hasUpdate)
        {
            return;
        }

        AddUpdate(updateNotice);
        _hasUpdate = true;
    }

    private void AddUpdate(TerminalUpdateNotice updateNotice)
    {
        var available = new StyledLabel(updateNotice.AvailableText, TextRole.Preview)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };

        var download = new StyledLabel(updateNotice.DownloadText, TextRole.Muted)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill()
        };

        var spacer = new View
        {
            X = 0,
            Y = 2,
            Width = 1,
            Height = 1
        };

        _updateArea.Add(available, download, spacer);
    }
}
