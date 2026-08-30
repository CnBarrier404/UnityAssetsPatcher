using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Features.Install;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class InstallModView : View, ITerminalRenderRequester
{
    public event EventHandler? RenderRequested;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LocalizedStrings _strings;
    private readonly AppRuntimeConfig _runtimeConfig;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Action _returnToMainMenu;
    private readonly ActionButton _selectModButton;
    private readonly WorkingIndicator _message;
    private readonly ScrollableContentView _form;
    private string? _modPath;
    private InputField? _gameDirectory;
    private View? _optionalGroupArea;
    private readonly List<ToggleItem> _optionalGroups = [];
    private PreparedInstall? _preparedInstall;
    private bool _isWorking;

    internal InstallModView(
        LocalizedStrings strings,
        IServiceScopeFactory scopeFactory,
        AppRuntimeConfig runtimeConfig,
        Action returnToMainMenu)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _strings = strings;
        _scopeFactory = scopeFactory;
        _runtimeConfig = runtimeConfig;
        _returnToMainMenu = returnToMainMenu;
        KeyDown += (_, key) =>
        {
            if (key != Key.Esc)
            {
                return;
            }

            key.Handled = true;

            if (_isWorking)
            {
                return;
            }

            _returnToMainMenu();
        };

        var heading = new StyledLabel(
            _strings.MainMenu_InstallMod_Title, TextRole.Title) { X = 0, Y = 0 };
        var description = new StyledLabel(
            _strings.MainMenu_InstallMod_Description, TextRole.Muted)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill()
        };

        _form = new ScrollableContentView
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };
        _form.SetContentHeightForRows(3);
        _selectModButton = new ActionButton(_strings.InstallPage_SelectModAction)
        {
            X = 0,
            Y = 0
        };
        _selectModButton.Accepted += async (_, _) => await SelectModFileAsync();
        _message = new WorkingIndicator
        {
            X = 0,
            Y = Pos.Bottom(_selectModButton) + 2,
            Width = Dim.Fill(),
            Visible = false
        };
        _form.Add(_selectModButton, _message);
        Add(heading, description, _form);
        Initialized += async (_, _) =>
        {
            RenderRequested?.Invoke(this, EventArgs.Empty);
            await SelectModFileAsync();
        };
        Disposing += (_, _) =>
        {
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        };
    }

    private async Task SelectModFileAsync()
    {
        if (_isWorking)
        {
            return;
        }

        string? selectedPath = WindowsNativeFilePicker.PickFile(
            _strings.InstallPage_SelectModDialogTitle,
            _strings.InstallPage_ModZipFileType);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            _returnToMainMenu();

            return;
        }

        await PreviewAsync(selectedPath);
    }

    private async Task PreviewAsync()
    {
        if (_modPath is null)
        {
            await SelectModFileAsync();

            return;
        }

        await PreviewAsync(_modPath);
    }

    private async Task PreviewAsync(string modPath)
    {
        if (_isWorking)
        {
            return;
        }

        _preparedInstall = null;
        ClearMessage();
        modPath = TerminalPathNormalizer.Normalize(modPath);
        if (string.IsNullOrWhiteSpace(modPath))
        {
            ShowModError(_strings.Prompt_LabelRequiredFormat(_strings.InstallPage_SelectModAction));

            return;
        }

        if (!File.Exists(modPath))
        {
            ShowModError(_strings.Prompt_FileNotFoundFormat(modPath));

            return;
        }

        _modPath = modPath;

        string? gameDirectory = _gameDirectory is null
            ? null
            : TerminalPathNormalizer.Normalize(_gameDirectory.Text);

        if (!string.IsNullOrEmpty(gameDirectory) && !Directory.Exists(gameDirectory))
        {
            ShowInputError(
                _gameDirectory!,
                _strings.Prompt_DirectoryNotFoundFormat(gameDirectory));

            return;
        }

        if (_gameDirectory is not null && gameDirectory is not null)
        {
            _gameDirectory.Text = gameDirectory;
        }

        IReadOnlyList<string> selectedGroups = _optionalGroups
            .Where(group => group.IsSelected)
            .Select(group => group.Name)
            .ToArray();

        var request = new InstallRequest(modPath, gameDirectory)
        {
            SelectedOptionalGroups = selectedGroups,
            IncludePatchPreviewDetails = false
        };

        _isWorking = true;
        _form.Enabled = false;
        ShowBusy(_strings.InstallPage_AnalyzingMod);
        RenderRequested?.Invoke(this, EventArgs.Empty);

        CancellationToken cancellationToken = _lifetimeCancellation.Token;

        try
        {
            var result = await DispatchAsync<
                PreviewInstallRequest,
                OperationResult<InstallPreviewResult>>(
                new PreviewInstallRequest(request),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            _form.Enabled = true;

            if (result is OperationFailed<InstallPreviewResult> failed)
            {
                string message = OperationErrorFormatter.Format(_strings, failed.Error);
                if ((failed.Error.Code == GameDirectoryErrorCodes.Required ||
                     failed.Error.Code == GameDirectoryErrorCodes.NotFound) &&
                    string.IsNullOrEmpty(gameDirectory))
                {
                    ShowGameDirectory(message);
                }
                else
                {
                    ShowModError(message);
                }

                return;
            }

            InstallPreviewResult preview = ((OperationSucceeded<InstallPreviewResult>)result).Value;
            _preparedInstall = preview.PreparedInstall;
            if (preview.OptionalGroups.Count > 0 && _optionalGroupArea is null)
            {
                ShowOptionalGroups(preview.OptionalGroups);
                ClearMessage();
                return;
            }

            ShowPreview(preview, modPath, gameDirectory, selectedGroups);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            ShowModError(OperationErrorFormatter.FormatUnexpected(_strings));
        }
        finally
        {
            _isWorking = false;
            if (!cancellationToken.IsCancellationRequested)
            {
                _form.Enabled = true;
            }
        }
    }

    private void ShowGameDirectory(string message)
    {
        if (_gameDirectory is null)
        {
            string prompt = $"{_strings.InstallPage_GameDirectoryPrompt}: ";
            Pos gameDirectoryRow = Pos.Bottom(_selectModButton) + 2;
            var label = new StyledLabel(prompt, TextRole.Label) { X = 0, Y = gameDirectoryRow };
            _gameDirectory = new InputField
            {
                X = prompt.GetColumns(),
                Y = gameDirectoryRow,
                Width = Dim.Fill()
            };
            _gameDirectory.Accepted += async (_, _) => await PreviewAsync();
            _message.Y = Pos.Bottom(_gameDirectory) + 1;
            _form.Add(label, _gameDirectory);
            _gameDirectory.SetFocus();
        }

        ShowInfo(message);
    }

    private void ShowOptionalGroups(IReadOnlyList<(string Name, string? Description)> groups)
    {
        Pos optionalGroupsRow = _gameDirectory is null
            ? Pos.Bottom(_selectModButton) + 2
            : Pos.Bottom(_gameDirectory) + 2;
        _optionalGroupArea = new View
        {
            X = 0,
            Y = optionalGroupsRow,
            Width = Dim.Fill(),
            Height = groups.Count * 2 + 4,
            CanFocus = true
        };
        var heading = new StyledLabel(
            _strings.InstallPage_OptionalGroupsHeader, TextRole.Preview) { X = 0, Y = 0 };
        _optionalGroupArea.Add(heading);

        for (int index = 0; index < groups.Count; index++)
        {
            (string name, string? description) = groups[index];
            int choiceRow = 2 + index * 2;
            var choice = new ToggleItem(name, description) { X = 0, Y = choiceRow };
            _optionalGroups.Add(choice);
            _optionalGroupArea.Add(choice);
        }

        int actionsRow = 3 + groups.Count * 2;
        var actions = new ConfirmationBar(
            _strings.InstallPage_SubmitAction,
            PreviewAsync,
            _strings.InstallPage_BackAction,
            _returnToMainMenu)
        {
            X = 0,
            Y = actionsRow
        };
        _optionalGroupArea.Add(actions);

        _message.Y = Pos.Bottom(_optionalGroupArea) + 1;
        _form.Add(_optionalGroupArea);
        _form.SetContentHeightForRows(actionsRow + 3);
        _optionalGroups[0].Button.SetFocus();
    }

    private void ShowPreview(
        InstallPreviewResult result,
        string modPath,
        string? gameDirectory,
        IReadOnlyList<string> selectedGroups)
    {
        _form.RemoveAllAndDispose(_message);
        _message.Y = 0;
        _message.Visible = false;
        _form.Add(_message);
        var summaryRows = GetPreviewSummaryRows(result);
        var status = new StyledLabel(
            _strings.InstallPreview_DryRunStatus, TextRole.Preview) { X = 0, Y = 0 };
        var summary = new SummaryTableView(summaryRows) { X = 0, Y = 2 };
        _form.Add(status, summary);

        PatchDiagnostic? diagnostic = result.Changes
            .Select(change => change.Preview?.Diagnostic)
            .FirstOrDefault(candidate => candidate is not null);
        if (diagnostic is not null)
        {
            string message = _strings.InstallPreview_PlanningFailedFormat(
                OperationErrorFormatter.Format(_strings, diagnostic));
            var error = new StyledLabel(message, TextRole.Error)
            {
                X = 0,
                Y = summaryRows.Length + 3,
                Width = Dim.Fill()
            };
            Button back = CreateActionButton(
                _strings.InstallPage_BackAction, 0, summaryRows.Length + 5);
            back.Accepted += (_, _) => _returnToMainMenu();
            _form.Add(error, back);
            _form.SetContentHeightForRows(summaryRows.Length + 7);
            back.SetFocus();
            return;
        }

        int nextRow = summaryRows.Length + 3;
        var patches = GetChanges(result.Changes, InstallChangeKind.Patch);
        if (patches.Length > 0)
        {
            nextRow = AddPreviewTargets(patches, nextRow);
        }

        string verboseText = FormatPreviewVerboseDetails(result, _runtimeConfig.VerboseLogging);
        if (!string.IsNullOrEmpty(verboseText))
        {
            int detailsHeight = GetReportHeight(verboseText);
            var output = new StyledLabel(verboseText)
            {
                X = 0,
                Y = nextRow,
                Width = Dim.Fill(),
                Height = detailsHeight
            };
            _form.Add(output);
            nextRow += detailsHeight + 1;
        }

        var actions = new ConfirmationBar(
            _strings.InstallPage_InstallAction,
            () => InstallAsync(modPath, gameDirectory, selectedGroups),
            _strings.InstallPage_BackAction,
            _returnToMainMenu)
        {
            X = 0,
            Y = nextRow + 1
        };
        _form.Add(actions);
        _form.SetContentHeightForRows(nextRow + 3);
        actions.ConfirmButton.SetFocus();
    }

    private async Task InstallAsync(
        string modPath,
        string? gameDirectory,
        IReadOnlyList<string> selectedGroups)
    {
        if (_isWorking)
        {
            return;
        }

        var request = new InstallRequest(modPath, gameDirectory)
        {
            SelectedOptionalGroups = selectedGroups,
            PreparedInstall = _preparedInstall
        };

        _isWorking = true;
        ShowWorking(_strings.InstallPage_InstallingMod);

        CancellationToken cancellationToken = _lifetimeCancellation.Token;

        try
        {
            var result = await DispatchAsync<
                InstallModRequest,
                OperationResult<InstallModResult>>(
                new InstallModRequest(request),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (result is OperationSucceeded<InstallModResult> succeeded)
            {
                ShowResult(succeeded.Value);
            }
            else
            {
                ShowResult(
                    OperationErrorFormatter.Format(
                        _strings,
                        ((OperationFailed<InstallModResult>)result).Error),
                    true);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            ShowResult(OperationErrorFormatter.FormatUnexpected(_strings), true);
        }
        finally
        {
            _isWorking = false;
        }
    }

    private void ShowWorking(string text)
    {
        _form.RemoveAllAndDispose(_message);
        _message.Y = 0;
        _form.Add(_message);
        ShowBusy(text);
        _form.SetContentHeightForRows(2);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task<TResponse> DispatchAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher
            .DispatchAsync<TRequest, TResponse>(request, cancellationToken);
    }

    private void ShowResult(InstallModResult result)
    {
        _form.RemoveAllAndDispose();
        var summaryRows = GetResultSummaryRows(result);
        string text = FormatResultDetails(result);
        int detailsHeight = string.IsNullOrEmpty(text) ? 0 : GetReportHeight(text);
        var status = new StyledLabel(
            _strings.InstallResult_InstalledStatus, TextRole.Success) { X = 0, Y = 0 };
        var summary = new SummaryTableView(summaryRows) { X = 0, Y = 2 };
        int detailsRow = summaryRows.Length + 3;
        int actionRow = detailsRow + detailsHeight + 1;
        Button back = CreateActionButton(_strings.InstallPage_ReturnAction, 0, actionRow);
        back.Accepted += (_, _) => _returnToMainMenu();
        _form.Add(status, summary);
        if (!string.IsNullOrEmpty(text))
        {
            _form.Add(new TextViewer(text)
            {
                X = 0,
                Y = detailsRow,
                Width = Dim.Fill(),
                Height = detailsHeight
            });
        }

        _form.Add(back);
        _form.SetContentHeightForRows(actionRow + 2);
        back.SetFocus();
    }

    private void ShowResult(string text, bool isError)
    {
        _form.RemoveAllAndDispose();
        int outputHeight = GetReportHeight(text);
        var output = new StyledLabel(
            text, isError ? TextRole.Error : TextRole.Base)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = outputHeight
        };
        Button back = CreateActionButton(_strings.InstallPage_ReturnAction, 0, outputHeight + 1);
        back.Accepted += (_, _) => _returnToMainMenu();
        _form.Add(output, back);
        _form.SetContentHeightForRows(outputHeight + 3);
        back.SetFocus();
    }

    private void ShowInfo(string message)
    {
        _message.Visible = true;
        _message.Still(message);
        _message.SetScheme(TerminalTheme.Muted);
    }

    private void ShowBusy(string message)
    {
        _message.Visible = true;
        _message.Spin(message);
        _message.SetScheme(TerminalTheme.Preview);
    }

    private void ShowError(string message)
    {
        _message.Visible = true;
        _message.Still(message);
        _message.SetScheme(TerminalTheme.Error);
    }

    private void ShowInputError(TextField input, string message)
    {
        input.Text = string.Empty;
        input.SetFocus();
        ShowError(message);
    }

    private void ShowModError(string message)
    {
        _selectModButton.SetFocus();
        ShowError(message);
    }

    private void ClearMessage()
    {
        _message.Still(string.Empty);
        _message.Visible = false;
    }

    private static ActionButton CreateActionButton(string text, Pos x, Pos y)
    {
        return new ActionButton(text) { X = x, Y = y };
    }

    private (string Label, string Value)[] GetPreviewSummaryRows(InstallPreviewResult result)
    {
        return
        [
            (_strings.Summary_Mod, result.ModName),
            (_strings.Summary_Version, result.ModVersion),
            (_strings.Summary_Author, result.ModAuthor)
        ];
    }

    private int AddPreviewTargets(IReadOnlyList<InstallChange> patches, int row)
    {
        var heading = new StyledLabel(
            _strings.InstallPreview_Targets, TextRole.SectionHeader) { X = 0, Y = row };
        _form.Add(heading);
        row += 2;

        foreach (InstallChange patch in patches)
        {
            string name = $"- {patch.Name}:";
            var nameLabel = new StyledLabel(name) { X = 0, Y = row };
            var pathLabel = new StyledLabel(patch.Path, TextRole.Muted)
            {
                X = name.GetColumns() + 1,
                Y = row,
                Width = Dim.Fill()
            };
            _form.Add(nameLabel, pathLabel);
            row++;
        }

        return row;
    }

    private string FormatPreviewVerboseDetails(InstallPreviewResult result, bool verbose)
    {
        if (!verbose)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        AppendTiming(text, result.Timing);
        return text.ToString().TrimEnd();
    }

    private (string Label, string Value)[] GetResultSummaryRows(InstallModResult result)
    {
        return
        [
            (_strings.Summary_Mod, result.ModName),
            (_strings.Summary_Version, result.ModVersion),
            (_strings.Summary_Elapsed, FormatElapsed(result.Timing.Elapsed))
        ];
    }

    private string FormatResultDetails(InstallModResult result)
    {
        var text = new StringBuilder();

        if (result.Changes.Count <= 0)
        {
            return text.ToString().TrimEnd();
        }

        text.AppendLine(_strings.InstallResult_OperatedFiles);

        foreach (InstallChange change in result.Changes)
        {
            string path = change.Kind == InstallChangeKind.Patch
                ? change.Name
                : Path.GetFileName(change.Path);
            text.Append("- ").AppendLine(path);
        }

        return text.ToString().TrimEnd();
    }

    private void AppendTiming(StringBuilder text, TimingSnapshot snapshot)
    {
        text.AppendLine().AppendLine(_strings.Install_TimingHeader);
        foreach (TimingStep step in snapshot.Steps)
        {
            text.Append(step.Name).Append("  ").AppendLine(FormatElapsed(step.Elapsed));
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return $"{elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s";
    }

    private static InstallChange[] GetChanges(IReadOnlyList<InstallChange> changes, InstallChangeKind kind)
    {
        return changes.Where(change => change.Kind == kind).ToArray();
    }

    private static int GetReportHeight(string text)
    {
        const int maximumVisibleLines = 20;
        int lineCount = text.Count(character => character == '\n') + 1;
        return Math.Min(lineCount, maximumVisibleLines);
    }
}
