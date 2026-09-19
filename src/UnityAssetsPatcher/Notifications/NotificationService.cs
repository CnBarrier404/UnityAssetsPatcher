using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;

namespace UnityAssetsPatcher.Notifications;

public sealed class NotificationService : ObservableObject, INotificationService, IDisposable
{
    public const int MaximumVisibleItems = 5;

    private readonly ObservableCollection<NotificationItem> _items = [];
    private readonly Queue<NotificationItem> _pending = new();
    private bool _disposed;

    public ReadOnlyObservableCollection<NotificationItem> Items { get; }

    public NotificationService()
    {
        Items = new ReadOnlyObservableCollection<NotificationItem>(_items);
    }

    public void Show(string title, string message, NotificationKind kind = NotificationKind.Information)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);

        _pending.Enqueue(new NotificationItem(
            TrimTrailingSentencePunctuation(title),
            TrimTrailingSentencePunctuation(message),
            kind,
            this));
        PresentPending();
    }

    private static string TrimTrailingSentencePunctuation(string value)
    {
        return value.TrimEnd('.', '。');
    }

    internal void Remove(NotificationItem item)
    {
        Dispatcher.UIThread.VerifyAccess();
        item.Stop();
        if (_items.Remove(item) && !_disposed)
        {
            PresentPending();
        }
    }

    private void PresentPending()
    {
        while (_items.Count < MaximumVisibleItems && _pending.TryDequeue(out NotificationItem? item))
        {
            _items.Add(item);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (NotificationItem item in _items.Concat(_pending))
        {
            item.Stop();
        }

        _pending.Clear();
    }
}
