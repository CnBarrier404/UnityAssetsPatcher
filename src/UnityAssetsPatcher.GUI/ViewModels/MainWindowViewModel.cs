using UnityAssetsPatcher.Application;

namespace UnityAssetsPatcher.GUI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public string Title => AppConfig.Name;
}
