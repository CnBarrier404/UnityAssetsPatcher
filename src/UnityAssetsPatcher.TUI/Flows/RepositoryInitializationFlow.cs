using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.TUI.Lifecycle;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Pages.RepositoryIssue;

namespace UnityAssetsPatcher.TUI.Flows;

internal sealed class RepositoryInitializationFlow
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LocalizedStrings _strings;

    public RepositoryInitializationFlow(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _strings = new LocalizedStrings(CultureInfo.CurrentUICulture);
    }

    public async Task RunAsync(TerminalFlowContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var logic = new RepositoryIssueLogic(_scopeFactory, cancellationToken);

        await logic.InitializeAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (logic.State is RepositoryIssueState.Ready)
        {
            return;
        }

        await context.InvokeAsync(
            () => context.ContentHost.ShowContent(new RepositoryIssueView(
                _strings,
                logic,
                context.RequestStop)),
            cancellationToken).ConfigureAwait(false);

        await logic.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
