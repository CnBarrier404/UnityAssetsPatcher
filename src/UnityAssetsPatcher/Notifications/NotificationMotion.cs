using Avalonia.Animation.Easings;

namespace UnityAssetsPatcher.Notifications;

internal static class NotificationMotion
{
    public static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(333);
    public static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(167);
    public static readonly TimeSpan RepositionDuration = TimeSpan.FromMilliseconds(250);
    public const double Spacing = 16;
    public const double EdgeMargin = 16;

    public static Easing DirectEasing()
    {
        return new SplineEasing(0, 0, 0, 1);
    }

    public static Easing RepositionEasing()
    {
        return new SplineEasing(0.55, 0.55, 0, 1);
    }
}
