using RentADeveloper.ResXLocalization;

namespace UnityAssetsPatcher.GUI.ViewModels;

public sealed class NavigationItemViewModel : ViewModelBase
{
    public string Title => Localizer.Current.Get(_titleKey);
    public ViewModelBase Page { get; }
    public bool IsPinned { get; }

    private readonly ResourceKey _titleKey;

    public NavigationItemViewModel(ResourceKey titleKey, ViewModelBase page, bool isPinned = false)
    {
        _titleKey = titleKey;
        Page = page;
        IsPinned = isPinned;
    }
}
