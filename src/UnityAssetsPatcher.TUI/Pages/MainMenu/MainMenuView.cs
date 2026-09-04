using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Navigation;

namespace UnityAssetsPatcher.TUI.Pages.MainMenu;

public sealed record MainMenuItem(string Title, string Description, TerminalRoute Route);

public sealed class MainMenuView : TerminalPageView
{
    protected override bool CanReturnToMainMenu => false;

    private readonly List<ChoiceItemList> _choices = [];
    private readonly View _updateArea;
    private readonly LocalizedStrings _strings;
    private readonly MainMenuLogic _logic;
    private bool _hasUpdate;

    internal MainMenuView(
        LocalizedStrings strings,
        IReadOnlyList<MainMenuItem> items,
        MainMenuLogic logic)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(logic);

        _strings = strings;
        _logic = logic;

        SetHeader(_strings.MainMenu_Title);

        _updateArea = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Auto()
        };

        Add(_updateArea);

        View previous = _updateArea;
        ActionButton? firstButton = null;

        foreach (MainMenuItem item in items)
        {
            var choice = new ChoiceItemList(item.Title, item.Description)
            {
                X = 0,
                Y = Pos.Bottom(previous) + (firstButton is null ? 0 : 1)
            };

            choice.Button.Accepted += (_, _) =>
                RequestNavigation(item.Route);
            _choices.Add(choice);

            Add(choice);

            firstButton ??= choice.Button;
            previous = choice;
        }

        ChoiceItemList.AlignDescriptions(_choices);

        Initialized += (_, _) =>
        {
            firstButton?.SetFocus();
            _logic.StartUpdateCheck();
            ShowAvailableUpdateIfKnown();
        };

        _logic.UpdateAvailable += OnUpdateAvailable;
        Disposing += (_, _) => _logic.UpdateAvailable -= OnUpdateAvailable;
    }

    private void ShowAvailableUpdateIfKnown()
    {
        if (_logic.AvailableUpdate is { } update)
        {
            ShowAvailableUpdate(update);
        }
    }

    private void OnUpdateAvailable(object? sender, EventArgs eventArgs)
    {
        if (IsDisposed || _logic.AvailableUpdate is null)
        {
            return;
        }

        IApplication? application = App;

        application?.Invoke(() =>
        {
            if (IsDisposed || _logic.AvailableUpdate is not { } update)
            {
                return;
            }

            ShowAvailableUpdate(update);
        });
    }

    private void ShowAvailableUpdate(UpdateInfo update)
    {
        if (_hasUpdate)
        {
            return;
        }

        AddUpdate(update);
        _hasUpdate = true;
        RequestRender();
    }

    private void AddUpdate(UpdateInfo update)
    {
        var available = new StyledLabel(
            _strings.Update_AvailableFormat(update.Version),
            TextRole.Preview)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };

        var download = new StyledLabel(
            _strings.Update_DownloadFormat(update.ReleaseUrl),
            TextRole.Muted)
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
