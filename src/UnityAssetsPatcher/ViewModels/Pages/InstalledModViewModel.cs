using System.Globalization;
using RentADeveloper.ResXLocalization;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Localization;

namespace UnityAssetsPatcher.ViewModels.Pages;

public sealed class InstalledModViewModel(InstallRecordSummary record)
{
    public string InstallId => record.InstallId;
    public string ModName => record.ModName;
    public string ModVersion => record.ModVersion;

    public string GameName => string.IsNullOrWhiteSpace(record.GameName)
        ? Localizer.Current.Get(StringsKeys.InstallPage_CustomGameDirectory)
        : record.GameName;

    public string InstalledAt => record.InstalledAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
