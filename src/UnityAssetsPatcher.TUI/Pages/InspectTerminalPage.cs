using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class InspectTerminalPage : ITerminalPage
{
    private const int DefaultLimit = 100;

    public string Title => LocalizedStrings.MainMenu_InspectAssets_Title;
    public string Description => LocalizedStrings.MainMenu_InspectAssets_Description;

    private readonly IWorkflowService _workflowService;
    private readonly TerminalPageChrome _chrome;
    private readonly InspectTerminalInput _input;
    private readonly InspectTerminalView _view;

    public InspectTerminalPage(
        IWorkflowService workflowService,
        TerminalPageChrome chrome,
        InspectTerminalInput input,
        InspectTerminalView view)
    {
        _workflowService = workflowService;
        _chrome = chrome;
        _input = input;
        _view = view;
    }

    public TerminalPageResult Run()
    {
        int? action = _input.ReadAction(0, (index, clear) => _view.WriteActions(Title, index, clear));

        return action switch
        {
            0 => RunList(),
            1 => RunFields(),
            _ => TerminalPageResult.ReturnToMenu(false),
        };
    }

    private TerminalPageResult RunList()
    {
        _chrome.ShowPage(LocalizedStrings.InspectPage_ListAssetsTitle,
            LocalizedStrings.InspectPage_ListAssetsDescription);
        string? assetsFilePath = _input.ReadAssetsFilePath();

        if (assetsFilePath is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        int? limitChoice = _input.ReadLimitChoice(_view.WriteLimitChoices);
        int? limit = limitChoice switch
        {
            0 => DefaultLimit,
            1 => null,
            2 => _input.ReadCustomLimit(),
            _ => null,
        };

        if (limitChoice is null || limitChoice == 2 && limit is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        _chrome.PrepareOutputArea();
        InspectListResult result = _workflowService.InspectList(
            new InspectListRequest(Path.GetFullPath(assetsFilePath), limit));
        _view.WriteAssets(result);

        return TerminalPageResult.ReturnToMenu();
    }

    private TerminalPageResult RunFields()
    {
        _chrome.ShowPage(LocalizedStrings.InspectPage_ShowFieldsTitle,
            LocalizedStrings.InspectPage_ShowFieldsDescription);
        string? assetsFilePath = _input.ReadAssetsFilePath();

        if (assetsFilePath is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        long? pathId = _input.ReadPathId();

        if (pathId is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        _chrome.PrepareOutputArea();
        AssetsFieldInfo result = _workflowService.InspectFields(
            new InspectFieldsRequest(Path.GetFullPath(assetsFilePath), pathId.Value));
        _view.WriteFields(result);

        return TerminalPageResult.ReturnToMenu();
    }
}
