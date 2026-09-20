using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace UnityAssetsPatcher.Views;

public partial class ContentDialog : UserControl
{
    private TaskCompletionSource<bool>? _completion;

    public ContentDialog()
    {
        InitializeComponent();

        AddHandler(KeyDownEvent, OnDialogKeyDown, RoutingStrategies.Tunnel);

        Loaded += OnDialogLoaded;
    }

    public async Task<bool> ShowAsync(
        Panel host, Control background, string title, string message, string primaryText, string closeText)
    {
        Dispatcher.UIThread.VerifyAccess();

        if (_completion is not null || host.Children.OfType<ContentDialog>().Any())
        {
            throw new InvalidOperationException("A content dialog is already open.");
        }

        TopLevel topLevel = TopLevel.GetTopLevel(host)
                            ?? throw new InvalidOperationException("The dialog host must be attached to a window.");

        IInputElement? previousFocus = topLevel.FocusManager.GetFocusedElement();

        bool wasEnabled = background.IsEnabled;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var window = topLevel as Window;

        void OnHostClosed(object? sender, EventArgs e)
        {
            completion.TrySetResult(false);
        }

        _completion = completion;
        DialogTitle.Text = title;
        DialogMessage.Text = message;
        PrimaryButton.Content = primaryText;
        CloseButton.Content = closeText;
        Avalonia.Automation.AutomationProperties.SetName(this, title);

        try
        {
            if (window is not null)
            {
                window.Closed += OnHostClosed;
            }

            background.SetCurrentValue(IsEnabledProperty, false);
            host.Children.Add(this);
            CloseButton.Focus();

            return await completion.Task;
        }
        finally
        {
            if (window is not null)
            {
                window.Closed -= OnHostClosed;
            }

            host.Children.Remove(this);
            background.SetCurrentValue(IsEnabledProperty, wasEnabled);
            _completion = null;
            previousFocus?.Focus();
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _completion?.TrySetResult(false);

        base.OnDetachedFromVisualTree(e);
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;

        _completion?.TrySetResult(true);
    }

    private void OnDialogLoaded(object? sender, RoutedEventArgs e)
    {
        if (_completion is not null)
        {
            CloseButton.Focus();
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;

        _completion?.TrySetResult(false);
    }

    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;

        _completion?.TrySetResult(false);
    }
}
