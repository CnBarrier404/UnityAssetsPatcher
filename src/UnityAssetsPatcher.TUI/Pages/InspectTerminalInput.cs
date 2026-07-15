using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class InspectTerminalInput
{
    private readonly TerminalPrompts _prompts;

    public InspectTerminalInput(TerminalPrompts prompts)
    {
        _prompts = prompts;
    }

    public int? ReadAction(int selectedIndex, Action<int, bool> render)
    {
        return _prompts.ReadChoiceIndex(2, selectedIndex, render);
    }

    public string? ReadAssetsFilePath()
    {
        return _prompts.ReadExistingFilePath(LocalizedStrings.InspectPage_AssetsFilePathPrompt);
    }

    public int? ReadLimitChoice(Action<int, bool> render)
    {
        return _prompts.ReadChoiceIndex(3, 0, render);
    }

    public int? ReadCustomLimit()
    {
        return _prompts.ReadPositiveInt(LocalizedStrings.InspectPage_MaximumRowsPrompt);
    }

    public long? ReadPathId()
    {
        return _prompts.ReadInt64(LocalizedStrings.InspectPage_PathIdPrompt);
    }
}
