using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.ViewModels.Pages;

public sealed class SettingsPageViewModel : ViewModelBase
{
    public static string LogDirectory => AppConfig.LogDirectory;

    public ICommand OpenLogDirectoryCommand { get; }

    public bool VerboseLogging
    {
        get => _runtimeConfig.VerboseLogging;
        set
        {
            if (_runtimeConfig.VerboseLogging == value)
            {
                return;
            }

            _runtimeConfig.VerboseLogging = value;
            _loggingLevelSwitch.MinimumLevel = value ? LoggingLevel.Debug : LoggingLevel.Information;
            OnPropertyChanged();
        }
    }

    private readonly AppRuntimeConfig _runtimeConfig;
    private readonly ILoggingLevelSwitch _loggingLevelSwitch;

    public SettingsPageViewModel(AppRuntimeConfig runtimeConfig, ILoggingLevelSwitch loggingLevelSwitch)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _loggingLevelSwitch = loggingLevelSwitch ?? throw new ArgumentNullException(nameof(loggingLevelSwitch));

        _loggingLevelSwitch.MinimumLevel = _runtimeConfig.VerboseLogging
            ? LoggingLevel.Debug
            : LoggingLevel.Information;

        OpenLogDirectoryCommand = new RelayCommand(OpenLogDirectory);
    }

    private void OpenLogDirectory()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = LogDirectory,
            UseShellExecute = true
        });
    }
}
