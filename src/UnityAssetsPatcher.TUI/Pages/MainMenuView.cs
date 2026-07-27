using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed record TerminalMenuItem(string Title, string Description, Func<Action, View> CreateView);

public sealed class MainMenuView : View
{
    public event EventHandler<TerminalMenuItem>? ItemSelected;

    private readonly List<ChoiceItem> _choices = [];
    private readonly View _updateArea;
    private bool _hasUpdate;

    public MainMenuView(IReadOnlyList<TerminalMenuItem> items, AvailableUpdate? availableUpdate,
        BackupRecoveryReport? recovery = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        recovery ??= BackupRecoveryReport.Clean;

        StyledLabel? recoveryLabel = null;

        if (recovery.Status != BackupRepositoryStatus.Clean)
        {
            string text = recovery.Status == BackupRepositoryStatus.Locked
                ? string.Format(LocalizedStrings.BackupRecovery_LockedFormat,
                    recovery.Issues.FirstOrDefault() is { } issue
                        ? OperationErrorFormatter.Format(issue)
                        : string.Empty)
                : string.Format(LocalizedStrings.BackupRecovery_RecoveredFormat, recovery.Operations.Count);

            recoveryLabel = new StyledLabel(text,
                recovery.Status == BackupRepositoryStatus.Locked ? TextRole.Error : TextRole.Preview)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
            };

            Add(recoveryLabel);
        }

        _updateArea = new View
        {
            X = 0,
            Y = recoveryLabel is null ? 0 : Pos.Bottom(recoveryLabel) + 1,
            Width = Dim.Fill(),
            Height = Dim.Auto(),
        };

        var heading = new StyledLabel(LocalizedStrings.MainMenu_Title, TextRole.Title)
        {
            X = 0,
            Y = Pos.Bottom(_updateArea),
        };

        Add(_updateArea, heading);

        if (availableUpdate is not null)
        {
            AddUpdate(availableUpdate);
            _hasUpdate = true;
        }

        View previous = heading;
        ActionButton? firstButton = null;

        foreach (TerminalMenuItem item in items)
        {
            var choice = new ChoiceItem(item.Title, item.Description)
            {
                X = 0,
                Y = Pos.Bottom(previous) + 1,
            };

            choice.Button.Accepted += (_, _) => ItemSelected?.Invoke(this, item);
            _choices.Add(choice);
            Add(choice);
            firstButton ??= choice.Button;
            previous = choice;
        }

        ChoiceItem.AlignDescriptions(_choices);
        Initialized += (_, _) => firstButton?.SetFocus();
    }

    public void ShowAvailableUpdate(AvailableUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (_hasUpdate)
        {
            return;
        }

        AddUpdate(update);
        _hasUpdate = true;
    }

    private void AddUpdate(AvailableUpdate update)
    {
        var available = new StyledLabel(
            string.Format(LocalizedStrings.Update_AvailableFormat, update.Version), TextRole.Preview)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
        };

        var download = new StyledLabel(string.Format(LocalizedStrings.Update_DownloadFormat, update.ReleaseUrl),
            TextRole.Muted)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
        };

        var spacer = new View { X = 0, Y = 2, Width = 1, Height = 1 };

        _updateArea.Add(available, download, spacer);
    }
}
