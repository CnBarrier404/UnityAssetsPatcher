namespace UnityAssetsPatcher.Notifications;

public interface INotificationService
{
    void Show(string title, string message, NotificationKind kind = NotificationKind.Information);
}
