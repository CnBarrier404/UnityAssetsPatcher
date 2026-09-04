using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class UninstallModView : TerminalPageView, ITerminalRenderRequester
{
    public event EventHandler? RenderRequested;

    protected override bool CanReturnToMainMenu => !_isWorking;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LocalizedStrings _strings;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly ScrollableContentView _body;
    private bool _isWorking;

    internal UninstallModView(
        LocalizedStrings strings,
        IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _strings = strings;
        _scopeFactory = scopeFactory;

        SetHeader(_strings.MainMenu_UninstallMod_Title, _strings.MainMenu_UninstallMod_Description);

        _body = new ScrollableContentView
            { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };

        Add(_body);

        Initialized += async (_, _) => await ShowInstalledModsAsync();
        Disposing += (_, _) =>
        {
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        };
    }

    private async Task ShowInstalledModsAsync()
    {
        if (_isWorking)
        {
            return;
        }

        _isWorking = true;
        _body.RemoveAllAndDispose();
        _body.SetContentHeightForRows(0);
        RenderRequested?.Invoke(this, EventArgs.Empty);

        CancellationToken cancellationToken = _lifetimeCancellation.Token;

        try
        {
            var installed = await DispatchAsync<
                ListInstalledModsRequest,
                OperationResult<IReadOnlyList<InstallRecordSummary>>>(
                new ListInstalledModsRequest(),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (installed is OperationSucceeded<IReadOnlyList<InstallRecordSummary>> succeeded)
            {
                ShowInstalledMods(succeeded.Value);
            }
            else
            {
                ShowError(OperationErrorFormatter.Format(
                    _strings,
                    ((OperationFailed<IReadOnlyList<InstallRecordSummary>>)installed).Error));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            ShowError(OperationErrorFormatter.FormatUnexpected(_strings));
        }
        finally
        {
            _isWorking = false;
        }
    }

    private void ShowInstalledMods(IReadOnlyList<InstallRecordSummary> installed)
    {
        _body.RemoveAllAndDispose();

        if (installed.Count == 0)
        {
            var message = new StyledLabel(
                _strings.UninstallPage_NoInstalledModsFound, TextRole.Preview)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill()
            };
            Button back = CreateActionButton(_strings.UninstallPage_ReturnAction, 0, 2);
            back.Accepted += (_, _) => RequestMainMenu();
            _body.Add(message, back);
            _body.SetContentHeightForRows(4);
            back.SetFocus();
            return;
        }

        int row = 0;
        var choices = new List<ChoiceItem>();
        foreach (InstallRecordSummary record in installed)
        {
            choices.Add(AddInstalledMod(record, row));
            row += 2;
        }

        ChoiceItem.AlignDescriptions(choices);
        _body.SetContentHeightForRows(row);
        choices[0].Button.SetFocus();
    }

    private ChoiceItem AddInstalledMod(InstallRecordSummary record, int row)
    {
        string installedAt = FormatInstalledAt(record.InstalledAt);
        string details = record.GameName is null ? installedAt : $"{installedAt} | {record.GameName}";
        var choice = new ChoiceItem($"{record.ModName} {record.ModVersion}", details) { X = 0, Y = row };
        choice.Button.Accepted += async (_, _) => await PreviewAsync(record.InstallId, null);
        _body.Add(choice);
        return choice;
    }

    private async Task PreviewAsync(string installId, string? gameDirectory)
    {
        if (_isWorking)
        {
            return;
        }

        _isWorking = true;
        ShowWorking(_strings.UninstallPage_AnalyzingMod);

        CancellationToken cancellationToken = _lifetimeCancellation.Token;

        try
        {
            var preview = await DispatchAsync<
                UninstallPreviewRequest,
                OperationResult<UninstallPreviewResult>>(
                new UninstallPreviewRequest(installId, gameDirectory),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (preview is OperationSucceeded<UninstallPreviewResult> succeeded)
            {
                ShowPreview(succeeded.Value);
                return;
            }

            OperationError error = ((OperationFailed<UninstallPreviewResult>)preview).Error;
            string message = OperationErrorFormatter.Format(_strings, error);
            if ((error.Code == GameDirectoryErrorCodes.Required ||
                 error.Code == GameDirectoryErrorCodes.NotFound) &&
                gameDirectory is null)
            {
                ShowGameDirectoryInput(installId, message);
            }
            else
            {
                ShowError(message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            ShowError(OperationErrorFormatter.FormatUnexpected(_strings));
        }
        finally
        {
            _isWorking = false;
        }
    }

    private void ShowWorking(string text)
    {
        _body.RemoveAllAndDispose();
        var status = new WorkingIndicator(text) { X = 0, Y = 0 };
        _body.Add(status);
        _body.SetContentHeightForRows(2);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowGameDirectoryInput(string installId, string message)
    {
        _body.RemoveAllAndDispose();
        var info = new StyledLabel(message, TextRole.Muted)
            { X = 0, Y = 0, Width = Dim.Fill() };
        string prompt = $"{_strings.InstallPage_GameDirectoryPrompt}: ";
        var label = new StyledLabel(prompt, TextRole.Label) { X = 0, Y = 2 };
        var input = new InputField { X = prompt.GetColumns(), Y = 2, Width = Dim.Fill() };
        var error = new StyledLabel(role: TextRole.Error)
            { X = 0, Y = 4, Width = Dim.Fill(), Visible = false };
        input.Accepted += async (_, _) =>
        {
            string path = TerminalPathNormalizer.Normalize(input.Text);
            if (!Directory.Exists(path))
            {
                input.Text = string.Empty;
                error.Text = _strings.Prompt_DirectoryNotFoundFormat(path);
                error.Visible = true;
                input.SetFocus();
                return;
            }

            input.Text = path;
            await PreviewAsync(installId, path);
        };
        Button back = CreateActionButton(_strings.UninstallPage_BackAction, 0, 6);
        back.Accepted += async (_, _) => await ShowInstalledModsAsync();
        _body.Add(info, label, input, error, back);
        _body.SetContentHeightForRows(8);
        input.SetFocus();
    }

    private void ShowPreview(UninstallPreviewResult preview)
    {
        _body.RemoveAllAndDispose();
        var status = new StyledLabel(
            _strings.UninstallPreview_Status, TextRole.Preview) { X = 0, Y = 0 };
        var rows = new (string Label, string Value)[]
        {
            (_strings.Summary_Mod, preview.ModName),
            (_strings.Summary_Version, preview.ModVersion),
            (_strings.UninstallSummary_GameDirectory, preview.GameDirectory),
            (_strings.UninstallSummary_Installed,
                FormatInstalledAt(preview.InstalledAt))
        };
        var summary = new SummaryTableView(rows) { X = 0, Y = 2 };
        string availability = preview.CanUninstall
            ? _strings.UninstallPreview_WillModify
            : preview.DependencyFailures.Count > 0
                ? _strings.UninstallPreview_CannotRemoveDependency
                : _strings.UninstallPreview_CannotRemoveIntegrity;
        var availabilityLabel = new StyledLabel(availability, preview.CanUninstall ? TextRole.Success : TextRole.Error)
        {
            X = 0,
            Y = rows.Length + 3,
            Width = Dim.Fill()
        };
        _body.Add(status, summary, availabilityLabel);

        int row = rows.Length + 5;
        row = AddChangedPreviewFiles(preview.ChangedFiles, row);
        row = AddDependencyFailures(preview.DependencyFailures, row);

        if (!preview.CanUninstall)
        {
            var error = new StyledLabel(
                preview.DependencyFailures.Count > 0
                    ? _strings.UninstallPage_CannotUninstallDependency
                    : _strings.UninstallPage_CannotUninstallIntegrityConflict,
                TextRole.Error)
            {
                X = 0,
                Y = row + 1,
                Width = Dim.Fill()
            };
            Button back = CreateActionButton(_strings.UninstallPage_BackAction, 0, row + 3);
            back.Accepted += async (_, _) => await ShowInstalledModsAsync();
            _body.Add(error, back);
            _body.SetContentHeightForRows(row + 5);
            back.SetFocus();
            return;
        }

        var actions = new ConfirmationBar(
            _strings.UninstallPage_UninstallAction,
            () => UninstallAsync(preview),
            _strings.UninstallPage_BackAction,
            ShowInstalledModsAsync,
            ActionKind.Dangerous)
        {
            X = 0,
            Y = row + 1
        };
        _body.Add(actions);
        _body.SetContentHeightForRows(row + 3);
        actions.ConfirmButton.SetFocus();
    }

    private int AddChangedPreviewFiles(IReadOnlyList<UninstallChangedFileResult> files, int row)
    {
        if (files.Count == 0)
        {
            return row;
        }

        foreach (UninstallChangedFileResult file in files)
        {
            var details = new StyledLabel($"- {file.RelativePath}", TextRole.Muted)
            {
                X = 0,
                Y = row,
                Width = Dim.Fill()
            };
            _body.Add(details);
            row++;
        }

        return row + 1;
    }

    private int AddDependencyFailures(IReadOnlyList<UninstallDependencyFailureResult> failures, int row)
    {
        if (failures.Count == 0)
        {
            return row;
        }

        row = AddSectionHeader(_strings.UninstallPreview_Dependencies, row);
        foreach (UninstallDependencyFailureResult failure in failures)
        {
            string diagnostic = $"{failure.Diagnostic.Code}: " +
                                OperationErrorFormatter.Format(_strings, failure.Diagnostic);
            _body.Add(new StyledLabel(
                _strings.UninstallPreview_DependencyDetailsFormat(
                    failure.ModName,
                    failure.ModVersion,
                    failure.RelativePath,
                    diagnostic))
            {
                X = 0,
                Y = row++,
                Width = Dim.Fill()
            });
        }

        return row + 1;
    }

    private int AddSectionHeader(string text, int row)
    {
        var heading = new StyledLabel(text, TextRole.SectionHeader)
            { X = 0, Y = row, Width = Dim.Fill() };
        _body.Add(heading);
        return row + 2;
    }

    private async Task UninstallAsync(UninstallPreviewResult preview)
    {
        if (_isWorking)
        {
            return;
        }

        _isWorking = true;
        ShowWorking(_strings.UninstallPage_UninstallingMod);

        CancellationToken cancellationToken = _lifetimeCancellation.Token;

        try
        {
            var result = await DispatchAsync<
                UninstallModRequest,
                OperationResult<UninstallModResult>>(
                new UninstallModRequest(preview.InstallId, preview.GameDirectory),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (result is OperationSucceeded<UninstallModResult> succeeded)
            {
                ShowResult(succeeded.Value);
            }
            else
            {
                ShowError(OperationErrorFormatter.Format(
                    _strings,
                    ((OperationFailed<UninstallModResult>)result).Error));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            ShowError(OperationErrorFormatter.FormatUnexpected(_strings));
        }
        finally
        {
            _isWorking = false;
        }
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

    private void ShowResult(UninstallModResult result)
    {
        _body.RemoveAllAndDispose();
        var status = new StyledLabel(
            _strings.UninstallResult_Status, TextRole.Success) { X = 0, Y = 0 };
        var rows = new (string Label, string Value)[]
        {
            (_strings.Summary_Mod, result.ModName),
            (_strings.Summary_Version, result.ModVersion),
            (_strings.UninstallSummary_ChangedFiles,
                result.ChangedFiles.Count.ToString(CultureInfo.InvariantCulture))
        };
        var summary = new SummaryTableView(rows) { X = 0, Y = 2 };
        _body.Add(status, summary);
        int row = rows.Length + 3;
        if (result.ChangedFiles.Count > 0)
        {
            row = AddSectionHeader(_strings.UninstallResult_ChangedFiles, row);
            foreach (UninstallChangedFileResult file in result.ChangedFiles)
            {
                _body.Add(new StyledLabel($"- {file.RelativePath}")
                    { X = 0, Y = row++, Width = Dim.Fill() });
            }

            row++;
        }

        Button back = CreateActionButton(_strings.UninstallPage_ReturnAction, 0, row);
        back.Accepted += (_, _) => RequestMainMenu();
        _body.Add(back);
        _body.SetContentHeightForRows(row + 2);
        back.SetFocus();
    }

    private void ShowError(string message)
    {
        _body.RemoveAllAndDispose();
        var error = new StyledLabel(message, TextRole.Error)
            { X = 0, Y = 0, Width = Dim.Fill() };
        Button back = CreateActionButton(_strings.UninstallPage_BackAction, 0, 2);
        back.Accepted += async (_, _) => await ShowInstalledModsAsync();
        _body.Add(error, back);
        _body.SetContentHeightForRows(4);
        back.SetFocus();
    }

    private static string FormatInstalledAt(DateTimeOffset installedAt)
    {
        return installedAt.LocalDateTime.ToString(
            "yyyy'/'MM'/'dd HH':'mm",
            CultureInfo.InvariantCulture);
    }

    private static ActionButton CreateActionButton(string text, Pos x, Pos y)
    {
        return new ActionButton(text) { X = x, Y = y };
    }
}
