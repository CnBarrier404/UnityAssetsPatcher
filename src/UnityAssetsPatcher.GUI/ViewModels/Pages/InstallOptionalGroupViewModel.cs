using CommunityToolkit.Mvvm.ComponentModel;

namespace UnityAssetsPatcher.GUI.ViewModels.Pages;

public sealed class InstallOptionalGroupViewModel : ViewModelBase
{
    public string Name { get; }
    public string? Description { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public InstallOptionalGroupViewModel(string name, string? description, bool isSelected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Description = description;
        _isSelected = isSelected;
    }
}
