using System.Globalization;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class InspectTerminalView
{
    private readonly TerminalUI _ui;

    public InspectTerminalView(TerminalUI ui)
    {
        _ui = ui;
    }

    public void WriteActions(string title, int selectedIndex, bool clear)
    {
        _ui.Layout.ShowPage(title, LocalizedStrings.InspectPage_Description, clear: clear);
        _ui.List.WriteDescribedList(
            [
                new TerminalChoiceDisplay(
                    LocalizedStrings.InspectPage_ListAssetsTitle,
                    LocalizedStrings.InspectPage_ListAssetsDescription),
                new TerminalChoiceDisplay(
                    LocalizedStrings.InspectPage_ShowFieldsTitle,
                    LocalizedStrings.InspectPage_ShowFieldsDescription),
            ],
            selectedIndex);
    }

    public void WriteLimitChoices(int selectedIndex, bool clear)
    {
        _ui.Layout.ShowPage(LocalizedStrings.InspectPage_RowsToPrintTitle, clear: clear);
        _ui.List.WriteDescribedList(
            [
                new TerminalChoiceDisplay(LocalizedStrings.InspectPage_First100Choice, string.Empty),
                new TerminalChoiceDisplay(LocalizedStrings.InspectPage_AllRowsChoice, string.Empty),
                new TerminalChoiceDisplay(LocalizedStrings.InspectPage_CustomLimitChoice, string.Empty),
            ],
            selectedIndex);
    }

    public void WriteAssets(InspectListResult result)
    {
        _ui.List.WriteAssets(
            result.Assets,
            LocalizedStrings.InspectPage_PathIdColumn,
            LocalizedStrings.InspectPage_TypeNameColumn,
            LocalizedStrings.InspectPage_NameColumn);

        if (result.Assets.Count < result.TotalCount)
        {
            _ui.Text.WriteBlankLine();
            _ui.Text.WriteInfo(string.Format(
                CultureInfo.CurrentUICulture,
                LocalizedStrings.InspectPage_ShowingAssetsFormat,
                result.Assets.Count,
                result.TotalCount));
        }
    }

    public void WriteFields(AssetsFieldInfo fieldTree)
    {
        _ui.Text.WriteAssetFields(fieldTree);
    }
}
