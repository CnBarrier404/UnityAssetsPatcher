using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Navigation;

public sealed class TerminalNavigator
{
    private readonly Dictionary<TerminalRoute, Func<TerminalPageView>> _pages;
    private readonly TerminalShellView _shell;
    private TerminalRoute? _currentRoute;
    private TerminalRoute? _pendingRoute;
    private bool _isNavigating;

    public TerminalNavigator(TerminalShellView shell, IReadOnlyDictionary<TerminalRoute, Func<TerminalPageView>> pages)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(pages);

        foreach (var createPage in pages.Values)
        {
            ArgumentNullException.ThrowIfNull(createPage, nameof(pages));
        }

        _shell = shell;
        _pages = pages.ToDictionary();
    }

    public void Navigate(TerminalRoute route)
    {
        if (_isNavigating)
        {
            _pendingRoute = route;

            return;
        }

        _isNavigating = true;

        try
        {
            TerminalRoute? nextRoute = route;

            while (nextRoute is { } requestedRoute)
            {
                _pendingRoute = null;
                NavigateCore(requestedRoute);
                nextRoute = _pendingRoute;
            }
        }
        finally
        {
            _isNavigating = false;
            _pendingRoute = null;
        }
    }

    private void NavigateCore(TerminalRoute route)
    {
        if (_currentRoute == route)
        {
            return;
        }

        if (!_pages.TryGetValue(route, out var createPage))
        {
            throw new InvalidOperationException(
                $"No terminal page is registered for route '{route}'.");
        }

        TerminalPageView content =
            createPage() ??
            throw new InvalidOperationException($"The terminal page factory for route '{route}' returned null.");

        content.NavigationRequested += OnNavigationRequested;

        _shell.ShowContent(content);
        _currentRoute = route;
    }

    private void OnNavigationRequested(object? sender, TerminalRoute route)
    {
        Navigate(route);
    }
}
