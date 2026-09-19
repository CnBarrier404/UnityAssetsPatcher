using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using UnityAssetsPatcher.Notifications;

namespace UnityAssetsPatcher.Views;

public partial class NotificationView : UserControl
{
    private readonly TranslateTransform _slide = new();
    private NotificationItem? _item;
    private CancellationTokenSource? _lifetime;
    private CancellationTokenSource? _animation;
    private TopLevel? _topLevel;
    private WeakReference<Control>? _returnFocus;

    public NotificationView()
    {
        InitializeComponent();
        RenderTransform = _slide;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        await RunNotificationAsync();
    }

    private async Task RunNotificationAsync()
    {
        if (DataContext is not NotificationItem item)
        {
            return;
        }

        using var lifetime = new CancellationTokenSource();
        CancellationToken cancellationToken = lifetime.Token;
        _lifetime = lifetime;
        _item = item;
        _topLevel = TopLevel.GetTopLevel(this);
        RememberFocus(_topLevel?.FocusManager.GetFocusedElement() as Control);
        _topLevel?.AddHandler(GotFocusEvent, OnTopLevelGotFocus, RoutingStrategies.Bubble, true);
        item.PropertyChanged += OnItemPropertyChanged;
        IsEnabled = !item.IsClosing;

        try
        {
            if (!item.IsClosing)
            {
                await AnimateAsync(true, item, cancellationToken);
                UpdatePause();
                item.Present();
            }

            await item.WaitForCloseAsync(cancellationToken);
            RestoreFocus();
            IsEnabled = false;
            await AnimateAsync(false, item, cancellationToken);
            item.CompleteClose();
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested && exception.CancellationToken == cancellationToken)
        {
            // Unloading owns cancellation of this view's presentation lifecycle.
        }
        finally
        {
            item.Detach();
            item.PropertyChanged -= OnItemPropertyChanged;
            _topLevel?.RemoveHandler(GotFocusEvent, OnTopLevelGotFocus);
            _topLevel = null;
            _item = null;
            _lifetime = null;
        }
    }

    private async Task AnimateAsync(bool entering, NotificationItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        double offset = Bounds.Width + NotificationMotion.EdgeMargin * 2;
        double targetX = entering ? 0 : offset;
        double targetOpacity = entering ? 1 : 0;

        using var animation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _animation = animation;
        try
        {
            TimeSpan duration = entering ? NotificationMotion.EntranceDuration : NotificationMotion.ExitDuration;
            double startX = entering ? offset : _slide.X;
            double startOpacity = entering ? 0 : Opacity;

            await Task.WhenAll(
                CreateAnimation(TranslateTransform.XProperty, startX, targetX, duration,
                    NotificationMotion.DirectEasing()).RunAsync(this, animation.Token),
                CreateAnimation(OpacityProperty, startOpacity, targetOpacity, duration,
                    new LinearEasing()).RunAsync(this, animation.Token));
        }
        finally
        {
            _animation = null;
        }

        // Avalonia 12 completes RunAsync successfully when its token is canceled.
        // Translate only this known behavior back to our owning lifecycle's token.
        cancellationToken.ThrowIfCancellationRequested();
        if (entering && item.IsClosing)
        {
            return;
        }

        _slide.X = targetX;
        Opacity = targetOpacity;
    }

    private static Animation CreateAnimation(
        AvaloniaProperty property, double from, double to, TimeSpan duration, Easing easing)
    {
        return new Animation
        {
            Duration = duration,
            Easing = easing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(property, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(property, to) } }
            }
        };
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _lifetime?.Cancel();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotificationItem.IsClosing) && _item?.IsClosing == true)
        {
            // Clicking Close during entry should reverse immediately, not wait for entry.
            _slide.X = _slide.X;
            Opacity = Opacity;
            RestoreFocus();
            IsEnabled = false;
            _animation?.Cancel();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty || change.Property == IsKeyboardFocusWithinProperty)
        {
            UpdatePause();
        }
    }

    private void UpdatePause()
    {
        _item?.SetPaused(IsPointerOver || IsKeyboardFocusWithin);
    }

    private void OnTopLevelGotFocus(object? sender, FocusChangedEventArgs e)
    {
        RememberFocus(e.Source as Control);
    }

    private void RememberFocus(Control? control)
    {
        if (control is not null && control is not NotificationView &&
            !control.GetVisualAncestors().OfType<NotificationView>().Any())
        {
            _returnFocus = new WeakReference<Control>(control);
        }
    }

    private void RestoreFocus()
    {
        if (!IsKeyboardFocusWithin)
        {
            return;
        }

        if (_returnFocus?.TryGetTarget(out Control? control) == true &&
            TopLevel.GetTopLevel(control) == _topLevel &&
            control is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } && control.Focus())
        {
            return;
        }

        // The initiating control may have disappeared during navigation.
        Control? fallback = _topLevel?.GetVisualDescendants().OfType<Control>().FirstOrDefault(candidate =>
            candidate.Focusable &&
            candidate is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } and not NotificationView &&
            !candidate.GetVisualAncestors().OfType<NotificationView>().Any());
        fallback?.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            _item?.Close();
            e.Handled = true;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        _item?.Close();
        e.Handled = true;
    }
}
