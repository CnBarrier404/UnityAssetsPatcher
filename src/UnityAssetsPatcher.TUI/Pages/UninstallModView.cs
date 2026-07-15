using System.Globalization;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class UninstallModView : View, ITerminalContentView, ITerminalRenderRequester
{
    public event EventHandler? RenderRequested;

    public string ShortcutHint => LocalizedStrings.Layout_ShortcutHint;

    private readonly IWorkflowService _workflowService;
    private readonly Action _returnToMainMenu;
    private readonly View _body;
    private bool _isWorking;

    public UninstallModView(IWorkflowService workflowService, Action returnToMainMenu)
    {
        _workflowService = workflowService;
        _returnToMainMenu = returnToMainMenu;

        KeyDown += (_, key) =>
        {
            if (key != Key.Esc || _isWorking)
            {
                return;
            }

            key.Handled = true;
            _returnToMainMenu();
        };

        var heading = new Label { Text = LocalizedStrings.MainMenu_UninstallMod_Title, X = 0, Y = 0 };
        heading.SetScheme(TerminalGUITheme.Title);
        var description = new Label
        {
            Text = LocalizedStrings.MainMenu_UninstallMod_Description,
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
        };
        description.SetScheme(TerminalGUITheme.Muted);
        _body = new View { X = 0, Y = 3, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        Add(heading, description, _body);

        ShowInstalledMods();
    }

    private void ShowInstalledMods()
    {
        _body.RemoveAll();
        try
        {
            IReadOnlyList<InstallRecordSummary> installed = _workflowService.ListInstalledMods();
            if (installed.Count == 0)
            {
                var message = new Label
                {
                    Text = LocalizedStrings.UninstallPage_NoInstalledModsFound,
                    X = 0,
                    Y = 0,
                    Width = Dim.Fill(),
                };
                message.SetScheme(TerminalGUITheme.Preview);
                Button back = CreateActionButton(LocalizedStrings.UninstallPage_ReturnAction, 0, 2);
                back.Accepted += (_, _) => _returnToMainMenu();
                _body.Add(message, back);
                back.SetFocus();
                return;
            }

            int row = 0;
            Button? firstButton = null;
            foreach (InstallRecordSummary record in installed)
            {
                Button button = AddInstalledMod(record, row);
                firstButton ??= button;
                row += 2;
            }

            firstButton!.SetFocus();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private Button AddInstalledMod(InstallRecordSummary record, int row)
    {
        string normalText = $"  {record.ModName} {record.ModVersion}";
        string focusedText = $"> {record.ModName} {record.ModVersion}";
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
        string installedAt = record.InstalledAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
        var details = new Label
        {
            Text = record.GameName is null ? installedAt : $"{installedAt} | {record.GameName}",
            X = 36,
            Y = row,
            Width = Dim.Fill(),
        };
        details.SetScheme(TerminalGUITheme.Muted);
        button.HasFocusChanged += (_, _) =>
        {
            button.Text = button.HasFocus ? focusedText : normalText;
            details.SetScheme(button.HasFocus ? TerminalGUITheme.Selected : TerminalGUITheme.Muted);
        };
        button.Accepted += (_, _) => Preview(record.InstallId, null);
        _body.Add(button, details);
        return button;
    }

    private void Preview(string installId, string? gameDirectory)
    {
        if (_isWorking)
        {
            return;
        }

        _isWorking = true;
        ShowWorking(LocalizedStrings.UninstallPage_AnalyzingMod);
        try
        {
            UninstallPreviewResult preview = _workflowService.PreviewUninstall(
                new UninstallPreviewRequest(installId, gameDirectory));
            ShowPreview(preview);
        }
        catch (DirectoryNotFoundException exception) when (gameDirectory is null)
        {
            ShowGameDirectoryInput(installId, exception.Message);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _isWorking = false;
        }
    }

    private void ShowWorking(string text)
    {
        _body.RemoveAll();
        var status = new Label { Text = text, X = 0, Y = 0, Width = Dim.Fill() };
        status.SetScheme(TerminalGUITheme.Preview);
        _body.Add(status);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowGameDirectoryInput(string installId, string message)
    {
        _body.RemoveAll();
        var info = new Label { Text = message, X = 0, Y = 0, Width = Dim.Fill() };
        info.SetScheme(TerminalGUITheme.Muted);
        string prompt = $"{LocalizedStrings.InstallPage_GameDirectoryPrompt}: ";
        var label = new Label { Text = prompt, X = 0, Y = 2 };
        label.SetScheme(TerminalGUITheme.Label);
        var input = new TextField { X = GetDisplayWidth(prompt), Y = 2, Width = Dim.Fill() };
        input.SetScheme(CreateInputScheme());
        var error = new Label { X = 0, Y = 4, Width = Dim.Fill(), Visible = false };
        error.SetScheme(TerminalGUITheme.Error);
        input.Accepted += (_, _) =>
        {
            string path = TerminalPathNormalizer.Normalize(input.Text);
            if (!Directory.Exists(path))
            {
                input.Text = string.Empty;
                error.Text = string.Format(LocalizedStrings.Prompt_DirectoryNotFoundFormat, path);
                error.Visible = true;
                input.SetFocus();
                return;
            }

            input.Text = path;
            Preview(installId, path);
        };
        Button back = CreateActionButton(LocalizedStrings.UninstallPage_BackAction, 0, 6);
        back.Accepted += (_, _) => ShowInstalledMods();
        _body.Add(info, label, input, error, back);
        input.SetFocus();
    }

    private void ShowPreview(UninstallPreviewResult preview)
    {
        _body.RemoveAll();
        var status = new Label { Text = LocalizedStrings.UninstallPreview_Status, X = 0, Y = 0 };
        status.SetScheme(TerminalGUITheme.Preview);
        var rows = new (string Label, string Value)[]
        {
            (LocalizedStrings.Summary_Mod, preview.ModName),
            (LocalizedStrings.Summary_Version, preview.ModVersion),
            (LocalizedStrings.UninstallSummary_GameDirectory, preview.GameDirectory),
            (LocalizedStrings.UninstallSummary_Installed,
                preview.InstalledAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)),
            (LocalizedStrings.UninstallSummary_RestoredFiles,
                preview.RestoredFiles.Count.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.UninstallSummary_PayloadFiles,
                preview.DeletedFiles.Count.ToString(CultureInfo.InvariantCulture)),
        };
        TableView summary = CreateSummaryTable(rows, 2);
        _body.Add(status, summary);

        int row = rows.Length + 3;
        row = AddRestoredPreviewFiles(preview.RestoredFiles, row);
        row = AddDeletedPreviewFiles(preview.DeletedFiles, row);
        row = AddBlockingMods(preview.BlockingMods, row);

        if (!preview.CanUninstall)
        {
            var error = new Label
            {
                Text = preview.BlockingMods.Count > 0
                    ? LocalizedStrings.UninstallPage_CannotUninstallBlockingMods
                    : LocalizedStrings.UninstallPage_CannotUninstallIntegrityConflict,
                X = 0,
                Y = row + 1,
                Width = Dim.Fill(),
            };
            error.SetScheme(TerminalGUITheme.Error);
            Button back = CreateActionButton(LocalizedStrings.UninstallPage_BackAction, 0, row + 3);
            back.Accepted += (_, _) => ShowInstalledMods();
            _body.Add(error, back);
            back.SetFocus();
            return;
        }

        Button uninstall = CreatePrimaryActionButton(LocalizedStrings.UninstallPage_UninstallAction, 0, row + 1);
        uninstall.Accepted += (_, _) => Uninstall(preview);
        Button backAction = CreateActionButton(LocalizedStrings.UninstallPage_BackAction, 0, row + 3);
        backAction.Accepted += (_, _) => ShowInstalledMods();
        _body.Add(uninstall, backAction);
        uninstall.SetFocus();
    }

    private int AddRestoredPreviewFiles(IReadOnlyList<UninstallPreviewRestoredFileResult> files, int row)
    {
        if (files.Count == 0) return row;
        row = AddSectionHeader(LocalizedStrings.UninstallPreview_FilesToRestore, row);
        foreach (UninstallPreviewRestoredFileResult file in files)
        {
            _body.Add(new Label { Text = $"- {file.Target}", X = 0, Y = row, Width = Dim.Fill() });
            var details = new Label
            {
                Text =
                    $"  {LocalizedStrings.UninstallPreview_CurrentFile} {FormatIntegrityStatus(file.TargetStatus)} | " +
                    $"{LocalizedStrings.UninstallPreview_BackupFile} {FormatIntegrityStatus(file.BackupStatus)}",
                X = 0,
                Y = row + 1,
                Width = Dim.Fill(),
            };
            details.SetScheme(TerminalGUITheme.Muted);
            _body.Add(details);
            row += 2;
        }

        return row + 1;
    }

    private int AddDeletedPreviewFiles(IReadOnlyList<UninstallPreviewDeletedFileResult> files, int row)
    {
        if (files.Count == 0) return row;
        row = AddSectionHeader(LocalizedStrings.UninstallPreview_PayloadFilesToDelete, row);
        foreach (UninstallPreviewDeletedFileResult file in files)
        {
            string state = file.Status == FileIntegrityStatus.Matches
                ? LocalizedStrings.UninstallPreview_WillDelete
                : FormatIntegrityStatus(file.Status);
            _body.Add(new Label
            {
                Text = $"- {Path.GetFileName(file.DestinationPath)}  {state}",
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
        row = AddSectionHeader(LocalizedStrings.UninstallPreview_BlockingMods, row);
        foreach (UninstallBlockingModResult mod in mods)
        {
            _body.Add(new Label
            {
                Text = $"- {mod.ModName} {mod.ModVersion}  " +
                       mod.InstalledAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture),
                X = 0,
                Y = row++,
                Width = Dim.Fill(),
            });
            foreach (string file in mod.OverlappingAssetsFiles)
            {
                var path = new Label { Text = $"  - {file}", X = 0, Y = row++, Width = Dim.Fill() };
                path.SetScheme(TerminalGUITheme.Muted);
                _body.Add(path);
            }
        }

        return row + 1;
    }

    private int AddSectionHeader(string text, int row)
    {
        var heading = new Label { Text = text, X = 0, Y = row, Width = Dim.Fill() };
        heading.SetScheme(TerminalGUITheme.SectionHeader);
        _body.Add(heading);
        return row + 2;
    }

    private void Uninstall(UninstallPreviewResult preview)
    {
        if (_isWorking) return;
        _isWorking = true;
        ShowWorking(LocalizedStrings.UninstallPage_UninstallingMod);
        try
        {
            UninstallModResult result = _workflowService.Uninstall(
                new UninstallModRequest(preview.InstallId, preview.GameDirectory));
            ShowResult(result);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _isWorking = false;
        }
    }

    private void ShowResult(UninstallModResult result)
    {
        _body.RemoveAll();
        var status = new Label { Text = LocalizedStrings.UninstallResult_Status, X = 0, Y = 0 };
        status.SetScheme(TerminalGUITheme.Success);
        var rows = new (string Label, string Value)[]
        {
            (LocalizedStrings.Summary_Mod, result.ModName),
            (LocalizedStrings.Summary_Version, result.ModVersion),
            (LocalizedStrings.UninstallSummary_RestoredFiles,
                result.RestoredFiles.Count.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.UninstallSummary_DeletedFiles,
                result.DeletedFiles.Count(file => file.Deleted).ToString(CultureInfo.InvariantCulture)),
        };
        TableView summary = CreateSummaryTable(rows, 2);
        _body.Add(status, summary);
        int row = rows.Length + 3;
        if (result.RestoredFiles.Count > 0)
        {
            row = AddSectionHeader(LocalizedStrings.UninstallResult_RestoredFiles, row);
            foreach (UninstallRestoredFileResult file in result.RestoredFiles)
            {
                _body.Add(new Label
                    { Text = $"- {file.Target}  {file.AssetsFilePath}", X = 0, Y = row++, Width = Dim.Fill() });
            }

            row++;
        }

        if (result.DeletedFiles.Count > 0)
        {
            row = AddSectionHeader(LocalizedStrings.UninstallResult_DeletedPayloadFiles, row);
            foreach (UninstallDeletedFileResult file in result.DeletedFiles)
            {
                string state = file.Deleted
                    ? LocalizedStrings.UninstallResult_Deleted
                    : LocalizedStrings.UninstallPreview_AlreadyMissing;
                _body.Add(new Label
                {
                    Text = $"- {Path.GetFileName(file.DestinationPath)}  {state}", X = 0, Y = row++, Width = Dim.Fill()
                });
            }

            row++;
        }

        Button back = CreateActionButton(LocalizedStrings.UninstallPage_ReturnAction, 0, row);
        back.Accepted += (_, _) => _returnToMainMenu();
        _body.Add(back);
        back.SetFocus();
    }

    private void ShowError(string message)
    {
        _body.RemoveAll();
        var error = new Label { Text = message, X = 0, Y = 0, Width = Dim.Fill() };
        error.SetScheme(TerminalGUITheme.Error);
        Button back = CreateActionButton(LocalizedStrings.UninstallPage_BackAction, 0, 2);
        back.Accepted += (_, _) => ShowInstalledMods();
        _body.Add(error, back);
        back.SetFocus();
    }

    private static string FormatIntegrityStatus(FileIntegrityStatus status) => status switch
    {
        FileIntegrityStatus.Matches => LocalizedStrings.UninstallPreview_Ready,
        FileIntegrityStatus.Missing => LocalizedStrings.UninstallPreview_AlreadyMissing,
        FileIntegrityStatus.Modified => LocalizedStrings.UninstallPreview_Modified,
        FileIntegrityStatus.Unreadable => LocalizedStrings.UninstallPreview_Unreadable,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static Button CreateActionButton(string text, Pos x, Pos y)
    {
        string normalText = $"  {text}";
        string focusedText = $"> {text}";
        var button = new Button
        {
            Text = normalText, X = x, Y = y, NoDecorations = true, NoPadding = true,
            ShadowStyle = ShadowStyles.None, TextAlignment = Alignment.Start,
        };
        button.SetScheme(CreateChoiceScheme());
        button.HasFocusChanged += (_, _) => button.Text = button.HasFocus ? focusedText : normalText;
        return button;
    }

    private static Button CreatePrimaryActionButton(string text, Pos x, Pos y)
    {
        Button button = CreateActionButton(text, x, y);
        Attribute normal = TerminalGUITheme.Error.Normal;
        Attribute selected = TerminalGUITheme.Selected.Normal;
        button.SetScheme(new Scheme
        {
            Normal = normal, Focus = selected, HotNormal = normal, HotFocus = selected, Active = selected,
            Editable = normal, ReadOnly = normal, Disabled = normal,
        });
        return button;
    }

    private static Scheme CreateChoiceScheme()
    {
        Attribute normal = TerminalGUITheme.Base.Normal;
        Attribute selected = TerminalGUITheme.Selected.Normal;
        return new Scheme
        {
            Normal = normal, Focus = selected, HotNormal = normal, HotFocus = selected, Active = selected,
            Editable = normal, ReadOnly = normal, Disabled = normal,
        };
    }

    private static Scheme CreateInputScheme() => CreateChoiceScheme();

    private static TableView CreateSummaryTable(IReadOnlyList<(string Label, string Value)> rows, int row)
    {
        const int gap = 3;
        int labelWidth = rows.Max(item => GetDisplayWidth(item.Label)) + gap;
        var table = new TableView
        {
            X = 0, Y = row, Width = Dim.Fill(), Height = rows.Count, CanFocus = false,
            BorderStyle = LineStyle.None,
            Table = new SummaryTableSource(rows),
            Style = new TableStyle
            {
                ShowHeaders = false, AlwaysShowHeaders = false, ShowHorizontalBottomLine = false,
                ShowHorizontalHeaderOverline = false, ShowHorizontalHeaderUnderline = false,
                ShowVerticalCellLines = false, ShowVerticalCellLineForFirstColumn = false,
                ShowVerticalCellLineForLastColumn = false, ShowVerticalHeaderLines = false,
                InvertSelectedCellFirstCharacter = false, ExpandLastColumn = false,
                ColumnStyles =
                {
                    [0] = new ColumnStyle
                    {
                        MinWidth = labelWidth, MaxWidth = labelWidth,
                        ColorGetter = _ => TerminalGUITheme.Muted,
                    },
                    [1] = new ColumnStyle { ColorGetter = _ => TerminalGUITheme.Base },
                },
            },
        };
        table.SetScheme(TerminalGUITheme.Base);
        return table;
    }

    private static int GetDisplayWidth(string value)
    {
        return value.Sum(character => character is >= '\u1100' and <= '\u115f' or >= '\u2e80' and <= '\ua4cf'
            or >= '\uac00' and <= '\ud7a3' or >= '\uf900' and <= '\ufaff' or >= '\ufe10' and <= '\ufe19'
            or >= '\ufe30' and <= '\ufe6f' or >= '\uff00' and <= '\uff60' or >= '\uffe0' and <= '\uffe6'
            ? 2
            : 1);
    }

    private sealed class SummaryTableSource : ITableSource
    {
        private readonly IReadOnlyList<(string Label, string Value)> _rows;
        public SummaryTableSource(IReadOnlyList<(string Label, string Value)> rows) => _rows = rows;
        public string[] ColumnNames => [string.Empty, string.Empty];
        public int Columns => 2;
        public int Rows => _rows.Count;
        public object this[int row, int col] => col == 0 ? _rows[row].Label : _rows[row].Value;
    }
}
