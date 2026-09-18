using RentADeveloper.ResXLocalization;

namespace UnityAssetsPatcher.GUI.ViewModels;

public sealed class NavigationViewItemViewModel : ViewModelBase
{
    public string Title => Localizer.Current.Get(_titleKey);
    public ViewModelBase Page { get; }
    public bool IsFooterItem { get; }

    private readonly ResourceKey _titleKey;

    public NavigationViewItemViewModel(ResourceKey titleKey, ViewModelBase page, bool isFooterItem = false)
    {
        _titleKey = titleKey;
        Page = page;
        IsFooterItem = isFooterItem;
    }
}
