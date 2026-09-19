using System.Diagnostics;
using Avalonia.Automation;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UnityAssetsPatcher.Notifications;

public sealed class NotificationItem : ObservableObject
{
    public string Title { get; }
    public string Message { get; }
    public NotificationKind Kind { get; }
    public bool IsInformation => Kind == NotificationKind.Information;
    public bool IsSuccess => Kind == NotificationKind.Success;
    public bool IsWarning => Kind == NotificationKind.Warning;
    public bool IsError => Kind == NotificationKind.Error;

    public AutomationLiveSetting LiveSetting => IsInformation
        ? AutomationLiveSetting.Polite
        : AutomationLiveSetting.Assertive;

    public bool IsPresented => _isPresented;
    public bool IsClosing => _isClosing;

    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(5);
    private readonly NotificationService _owner;
    private readonly DispatcherTimer _timer = new();
    private readonly Stopwatch _elapsed = new();
    private readonly TaskCompletionSource _closeRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TimeSpan _remaining = Lifetime;
    private bool _isPresented;
    private bool _isPaused;
    private bool _isClosing;
    private bool _stopped;

    internal NotificationItem(string title, string message, NotificationKind kind, NotificationService owner)
    {
        Title = title;
        Message = message;
        Kind = kind;
        _owner = owner;
        _timer.Tick += OnTimerTick;
    }

    // Time spent queued or entering is not reading time.
    internal void Present()
    {
        if (_stopped || IsClosing)
        {
            return;
        }

        SetProperty(ref _isPresented, true, nameof(IsPresented));
        UpdateTimer();
    }

    internal void SetPaused(bool paused)
    {
        _isPaused = paused;
        UpdateTimer();
    }

    internal void Detach()
    {
        SetProperty(ref _isPresented, false, nameof(IsPresented));
        PauseTimer();
    }

    public void Close()
    {
        if (_stopped || IsClosing)
        {
            return;
        }

        PauseTimer();
        SetProperty(ref _isClosing, true, nameof(IsClosing));
        _closeRequested.TrySetResult();
    }

    internal Task WaitForCloseAsync(CancellationToken cancellationToken)
    {
        return _closeRequested.Task.WaitAsync(cancellationToken);
    }

    internal void CompleteClose()
    {
        _owner.Remove(this);
    }

    internal void Stop()
    {
        _stopped = true;
        PauseTimer();
        _timer.Tick -= OnTimerTick;
        _closeRequested.TrySetResult();
    }

    private void UpdateTimer()
    {
        if (_stopped || _isPaused || !IsPresented || IsClosing)
        {
            PauseTimer();
            return;
        }

        if (_timer.IsEnabled)
        {
            return;
        }

        if (_remaining <= TimeSpan.Zero)
        {
            Close();
            return;
        }

        _timer.Interval = _remaining;
        _elapsed.Restart();
        _timer.Start();
    }

    private void PauseTimer()
    {
        _timer.Stop();
        _elapsed.Stop();
        _remaining -= _elapsed.Elapsed;
        _elapsed.Reset();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Close();
    }
}
