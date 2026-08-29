using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Lifecycle;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed record TerminalMenuItem(string Title, string Description, Func<Action, View> CreateView);

public sealed class MainMenuView : View, ITerminalRenderRequester
{
    public event EventHandler<TerminalMenuItem>? ItemSelected;
    public event EventHandler? RenderRequested;

    private readonly List<ChoiceItemList> _choices = [];
    private readonly View _updateArea;
    private readonly LocalizedStrings? _strings;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ITerminalUIDispatcher? _uiDispatcher;
    private readonly CancellationTokenSource? _updateCancellation;
    private bool _hasUpdate;

    public MainMenuView(
        string title,
        IReadOnlyList<TerminalMenuItem> items)
        : this(title, items, null, null, null) { }

    internal MainMenuView(
        LocalizedStrings strings,
        IReadOnlyList<TerminalMenuItem> items,
        IServiceScopeFactory scopeFactory,
        ITerminalUIDispatcher uiDispatcher)
        : this(strings.MainMenu_Title, items, strings, scopeFactory, uiDispatcher) { }

    private MainMenuView(
        string title,
        IReadOnlyList<TerminalMenuItem> items,
        LocalizedStrings? strings,
        IServiceScopeFactory? scopeFactory,
        ITerminalUIDispatcher? uiDispatcher)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(items);

        _strings = strings;
        _scopeFactory = scopeFactory;
        _uiDispatcher = uiDispatcher;

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

        if (_scopeFactory is not null && _uiDispatcher is not null)
        {
            _updateCancellation = new CancellationTokenSource();
            Initialized += (_, _) => _ = CheckForUpdateAsync(_updateCancellation.Token);
            Disposing += (_, _) => _updateCancellation.Cancel();
        }
    }

    private async Task CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory!.CreateScope();
            var updateCheckModule = scope.ServiceProvider.GetRequiredService<UpdateCheckModule>();
            var result = await updateCheckModule
                .CheckForUpdateAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result is OperationSucceeded<UpdateInfo?> { Value: { } update })
            {
                _uiDispatcher!.TryInvoke(() => ShowAvailableUpdate(update), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch { }
    }

    private void ShowAvailableUpdate(UpdateInfo update)
    {
        if (_hasUpdate)
        {
            return;
        }

        AddUpdate(update);
        _hasUpdate = true;
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddUpdate(UpdateInfo update)
    {
        var available = new StyledLabel(
            _strings!.Update_AvailableFormat(update.Version),
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
