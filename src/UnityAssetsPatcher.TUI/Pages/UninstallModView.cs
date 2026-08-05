using System.Globalization;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class UninstallModView : View, ITerminalRenderRequester
{
    public event EventHandler? RenderRequested;

    private readonly IWorkflowService _workflowService;
    private readonly LocalizedStrings _strings;
    private readonly TerminalTaskRunner _taskRunner;
    private readonly Action _returnToMainMenu;
    private readonly ScrollableContentView _body;
    private bool _isWorking;

    internal UninstallModView(
        LocalizedStrings strings,
        IWorkflowService workflowService,
        TerminalTaskRunner taskRunner,
        Action returnToMainMenu)
    {
        ArgumentNullException.ThrowIfNull(strings);

        _strings = strings;
        _workflowService = workflowService;
        _taskRunner = taskRunner;
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

        var heading = new StyledLabel(_strings.MainMenu_UninstallMod_Title, TextRole.Title)
        {
            X = 0, Y = 0
        };

        var description = new StyledLabel(_strings.MainMenu_UninstallMod_Description, TextRole.Muted)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
        };

        _body = new ScrollableContentView
            { X = 0, Y = 3, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };

        Add(heading, description, _body);

        ShowInstalledMods();
    }

    private void ShowInstalledMods()
    {
        if (_isWorking)
        {
            return;
        }

        bool started = _taskRunner.TryRun(
            _workflowService.ListInstalledMods,
            installed =>
            {
                _isWorking = false;
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
            },
            exception =>
            {
                _isWorking = false;
                ShowError(OperationErrorFormatter.FormatUnexpected(_strings));
            });

        if (!started)
        {
            return;
        }

        _isWorking = true;
        _body.RemoveAll();
        _body.SetContentHeightForRows(0);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowInstalledMods(IReadOnlyList<InstallRecordSummary> installed)
    {
        _body.RemoveAll();

        if (installed.Count == 0)
        {
            var message = new StyledLabel(
                _strings.UninstallPage_NoInstalledModsFound, TextRole.Preview)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
            };
            Button back = CreateActionButton(_strings.UninstallPage_ReturnAction, 0, 2);
            back.Accepted += (_, _) => _returnToMainMenu();
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
        choice.Button.Accepted += (_, _) => Preview(record.InstallId, null);
        _body.Add(choice);
        return choice;
    }

    private void Preview(string installId, string? gameDirectory)
    {
        if (_isWorking)
        {
            return;
        }

        bool started = _taskRunner.TryRun(
            () => _workflowService.PreviewUninstall(
                new UninstallPreviewRequest(installId, gameDirectory)),
            preview =>
            {
                _isWorking = false;
                if (preview is OperationSucceeded<UninstallPreviewResult> succeeded)
                {
                    ShowPreview(succeeded.Value);
                    return;
                }

                OperationError error = ((OperationFailed<UninstallPreviewResult>)preview).Error;
                string message = OperationErrorFormatter.Format(_strings, error);
                if ((error.Code == WorkflowErrorCodes.GameDirectoryRequired ||
                     error.Code == WorkflowErrorCodes.GameDirectoryNotFound) &&
                    gameDirectory is null)
                {
                    ShowGameDirectoryInput(installId, message);
                }
                else
                {
                    ShowError(message);
                }
            },
            exception =>
            {
                _isWorking = false;

                ShowError(OperationErrorFormatter.FormatUnexpected(_strings));
            });

        if (!started)
        {
            return;
        }

        _isWorking = true;
        ShowWorking(_strings.UninstallPage_AnalyzingMod);
    }

    private void ShowWorking(string text)
    {
        _body.RemoveAll();
        var status = new WorkingIndicator(text) { X = 0, Y = 0 };
        _body.Add(status);
        _body.SetContentHeightForRows(2);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowGameDirectoryInput(string installId, string message)
    {
        _body.RemoveAll();
        var info = new StyledLabel(message, TextRole.Muted)
            { X = 0, Y = 0, Width = Dim.Fill() };
        string prompt = $"{_strings.InstallPage_GameDirectoryPrompt}: ";
        var label = new StyledLabel(prompt, TextRole.Label) { X = 0, Y = 2 };
        var input = new InputField { X = prompt.GetColumns(), Y = 2, Width = Dim.Fill() };
        var error = new StyledLabel(role: TextRole.Error)
            { X = 0, Y = 4, Width = Dim.Fill(), Visible = false };
        input.Accepted += (_, _) =>
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
            Preview(installId, path);
        };
        Button back = CreateActionButton(_strings.UninstallPage_BackAction, 0, 6);
        back.Accepted += (_, _) => ShowInstalledMods();
        _body.Add(info, label, input, error, back);
        _body.SetContentHeightForRows(8);
        input.SetFocus();
    }

    private void ShowPreview(UninstallPreviewResult preview)
    {
        _body.RemoveAll();
        var status = new StyledLabel(
            _strings.UninstallPreview_Status, TextRole.Preview) { X = 0, Y = 0 };
        var rows = new (string Label, string Value)[]
        {
            (_strings.Summary_Mod, preview.ModName),
            (_strings.Summary_Version, preview.ModVersion),
            (_strings.UninstallSummary_GameDirectory, preview.GameDirectory),
            (_strings.UninstallSummary_Installed,
                FormatInstalledAt(preview.InstalledAt)),
            (_strings.UninstallSummary_RestoredFiles,
                preview.RestoredFiles.Count.ToString(CultureInfo.InvariantCulture)),
            (_strings.UninstallSummary_PayloadFiles,
                preview.DeletedFiles.Count.ToString(CultureInfo.InvariantCulture)),
        };
        var summary = new SummaryTableView(rows) { X = 0, Y = 2 };
        _body.Add(status, summary);

        int row = rows.Length + 3;
        row = AddRestoredPreviewFiles(preview.RestoredFiles, row);
        row = AddDeletedPreviewFiles(preview.DeletedFiles, row);
        row = AddBlockingMods(preview.BlockingMods, row);

        if (!preview.CanUninstall)
        {
            var error = new StyledLabel(
                preview.BlockingMods.Count > 0
                    ? _strings.UninstallPage_CannotUninstallBlockingMods
                    : _strings.UninstallPage_CannotUninstallIntegrityConflict,
                TextRole.Error)
            {
                X = 0,
                Y = row + 1,
                Width = Dim.Fill(),
            };
            Button back = CreateActionButton(_strings.UninstallPage_BackAction, 0, row + 3);
            back.Accepted += (_, _) => ShowInstalledMods();
            _body.Add(error, back);
            _body.SetContentHeightForRows(row + 5);
            back.SetFocus();
            return;
        }

        var actions = new ConfirmationBar(
            _strings.UninstallPage_UninstallAction,
            () => Uninstall(preview),
            _strings.UninstallPage_BackAction,
            ShowInstalledMods,
            ActionKind.Dangerous)
        {
            X = 0,
            Y = row + 1,
        };
        _body.Add(actions);
        _body.SetContentHeightForRows(row + 3);
        actions.ConfirmButton.SetFocus();
    }

    private int AddRestoredPreviewFiles(IReadOnlyList<UninstallPreviewRestoredFileResult> files, int row)
    {
        if (files.Count == 0) return row;
        row = AddSectionHeader(_strings.UninstallPreview_FilesToRestore, row);
        foreach (UninstallPreviewRestoredFileResult file in files)
        {
            _body.Add(new StyledLabel($"- {file.Target}") { X = 0, Y = row, Width = Dim.Fill() });
            var details = new StyledLabel(
                $"  {_strings.UninstallPreview_CurrentFile} {FormatIntegrityStatus(file.TargetStatus)} | " +
                $"{_strings.UninstallPreview_BackupFile} {FormatIntegrityStatus(file.BackupStatus)}",
                TextRole.Muted)
            {
                X = 0,
                Y = row + 1,
                Width = Dim.Fill(),
            };
            _body.Add(details);
            row += 2;
        }

        return row + 1;
    }

    private int AddDeletedPreviewFiles(IReadOnlyList<UninstallPreviewDeletedFileResult> files, int row)
    {
        if (files.Count == 0) return row;
        row = AddSectionHeader(_strings.UninstallPreview_PayloadFilesToDelete, row);
        foreach (UninstallPreviewDeletedFileResult file in files)
        {
            string state = file.Status == FileIntegrityStatus.Matches
                ? _strings.UninstallPreview_WillDelete
                : FormatIntegrityStatus(file.Status);
            _body.Add(new StyledLabel($"- {Path.GetFileName(file.DestinationPath)}  {state}")
            {
                X = 0,
                Y = row++,
                Width = Dim.Fill(),
            });
        }

        return row + 1;
    }

    private int AddBlockingMods(IReadOnlyList<UninstallBlockingModResult> mods, int row)
    {
        if (mods.Count == 0) return row;
        row = AddSectionHeader(_strings.UninstallPreview_BlockingMods, row);
        foreach (UninstallBlockingModResult mod in mods)
        {
            _body.Add(new StyledLabel(
                $"- {mod.ModName} {mod.ModVersion}  " +
                FormatInstalledAt(mod.InstalledAt))
            {
                X = 0,
                Y = row++,
                Width = Dim.Fill(),
            });
            foreach (string file in mod.OverlappingAssetsFiles)
            {
                var path = new StyledLabel($"  - {file}", TextRole.Muted)
                    { X = 0, Y = row++, Width = Dim.Fill() };
                _body.Add(path);
            }
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

    private void Uninstall(UninstallPreviewResult preview)
    {
        if (_isWorking) return;
        bool started = _taskRunner.TryRun(
            () => _workflowService.Uninstall(
                new UninstallModRequest(preview.InstallId, preview.GameDirectory)),
            result =>
            {
                _isWorking = false;
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
            },
            exception =>
            {
                _isWorking = false;
                ShowError(OperationErrorFormatter.FormatUnexpected(_strings));
            });

        if (!started)
        {
            return;
        }

        _isWorking = true;
        ShowWorking(_strings.UninstallPage_UninstallingMod);
    }

    private void ShowResult(UninstallModResult result)
    {
        _body.RemoveAll();
        var status = new StyledLabel(
            _strings.UninstallResult_Status, TextRole.Success) { X = 0, Y = 0 };
        var rows = new (string Label, string Value)[]
        {
            (_strings.Summary_Mod, result.ModName),
            (_strings.Summary_Version, result.ModVersion),
            (_strings.UninstallSummary_RestoredFiles,
                result.RestoredFiles.Count.ToString(CultureInfo.InvariantCulture)),
            (_strings.UninstallSummary_DeletedFiles,
                result.DeletedFiles.Count(file => file.Deleted).ToString(CultureInfo.InvariantCulture)),
        };
        var summary = new SummaryTableView(rows) { X = 0, Y = 2 };
        _body.Add(status, summary);
        int row = rows.Length + 3;
        if (result.RestoredFiles.Count > 0)
        {
            row = AddSectionHeader(_strings.UninstallResult_RestoredFiles, row);
            foreach (UninstallRestoredFileResult file in result.RestoredFiles)
            {
                _body.Add(new StyledLabel($"- {file.Target}  {file.AssetsFilePath}")
                    { X = 0, Y = row++, Width = Dim.Fill() });
            }

            row++;
        }

        if (result.DeletedFiles.Count > 0)
        {
            row = AddSectionHeader(_strings.UninstallResult_DeletedPayloadFiles, row);
            foreach (UninstallDeletedFileResult file in result.DeletedFiles)
            {
                string state = file.Deleted
                    ? _strings.UninstallResult_Deleted
                    : _strings.UninstallPreview_AlreadyMissing;
                _body.Add(new StyledLabel($"- {Path.GetFileName(file.DestinationPath)}  {state}")
                {
                    X = 0, Y = row++, Width = Dim.Fill()
                });
            }

            row++;
        }

        Button back = CreateActionButton(_strings.UninstallPage_ReturnAction, 0, row);
        back.Accepted += (_, _) => _returnToMainMenu();
        _body.Add(back);
        _body.SetContentHeightForRows(row + 2);
        back.SetFocus();
    }

    private void ShowError(string message)
    {
        _body.RemoveAll();
        var error = new StyledLabel(message, TextRole.Error)
            { X = 0, Y = 0, Width = Dim.Fill() };
        Button back = CreateActionButton(_strings.UninstallPage_BackAction, 0, 2);
        back.Accepted += (_, _) => ShowInstalledMods();
        _body.Add(error, back);
        _body.SetContentHeightForRows(4);
        back.SetFocus();
    }

    private string FormatIntegrityStatus(FileIntegrityStatus status) => status switch
    {
        FileIntegrityStatus.Matches => _strings.UninstallPreview_Ready,
        FileIntegrityStatus.Missing => _strings.UninstallPreview_AlreadyMissing,
        FileIntegrityStatus.Modified => _strings.UninstallPreview_Modified,
        FileIntegrityStatus.Unreadable => _strings.UninstallPreview_Unreadable,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

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
