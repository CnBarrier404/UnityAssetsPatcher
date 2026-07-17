using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class BackupRecoveryView : View
{
    public BackupRecoveryView()
    {
        Add(new StyledLabel(LocalizedStrings.BackupRecovery_Running, TextRole.Preview)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
        });
    }
}
