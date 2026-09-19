using RentADeveloper.ResXLocalization;

namespace UnityAssetsPatcher.ViewModels;

public sealed class NavigationViewItemViewModel : ViewModelBase
{
    public string Title => Localizer.Current.Get(_titleKey);
    public ViewModelBase Page { get; }

    private readonly ResourceKey _titleKey;

    public NavigationViewItemViewModel(ResourceKey titleKey, ViewModelBase page)
    {
        _titleKey = titleKey;
        Page = page;
    }
}
