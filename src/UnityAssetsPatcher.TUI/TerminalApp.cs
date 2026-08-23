using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Lifecycle;

namespace UnityAssetsPatcher.TUI;

public sealed class TerminalApp
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TerminalApp> _logger;

    public TerminalApp(
        IServiceScopeFactory scopeFactory,
        ILogger<TerminalApp> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<int> RunAsync()
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredService<TerminalSession>();

            await session.RunAsync().ConfigureAwait(false);

            return 0;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Terminal application terminated unexpectedly");

            var strings = new LocalizedStrings(CultureInfo.CurrentUICulture);
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UnityAssetsPatcher",
                "logs");

            Console.Error.WriteLine(strings.Error_UnexpectedFormat(logDirectory));

            return 1;
        }
    }
}
