using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class ScrollableContentView : View
{
    private const int MouseWheelRows = 3;

    public ScrollableContentView()
    {
        CanFocus = true;
        ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        MouseEvent += (_, mouse) => HandleMouseWheel(mouse);

        KeyDown += (_, key) =>
        {
            int pageSize = Math.Max(1, Viewport.Height - 1);

            if (key == Key.PageUp)
            {
                key.Handled = ScrollVertical(-pageSize) == true;
            }
            else if (key == Key.PageDown)
            {
                key.Handled = ScrollVertical(pageSize) == true;
            }
            else if (key == Key.Home)
            {
                key.Handled = ScrollVertical(-GetContentHeight()) == true;
            }
            else if (key == Key.End)
            {
                key.Handled = ScrollVertical(GetContentHeight()) == true;
            }
        };
    }

    public void SetContentHeightForRows(int rowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        SetContentHeight(rowCount);
        ScrollVertical(-GetContentHeight());
        EnsureFocusedViewIsVisible();
    }

    protected override void OnSubViewAdded(View view)
    {
        base.OnSubViewAdded(view);
        TrackFocus(view);
    }

    private void EnsureFocusedViewIsVisible()
    {
        View? focused = MostFocused;

        if (focused is null || ReferenceEquals(focused, this) || Viewport.Height <= 0)
        {
            return;
        }

        Rectangle focusedFrame = focused.FrameToScreen();
        Rectangle visibleFrame = ViewportToScreen();

        if (focusedFrame.Top < visibleFrame.Top)
        {
            ScrollVertical(focusedFrame.Top - visibleFrame.Top);
        }
        else if (focusedFrame.Bottom > visibleFrame.Bottom)
        {
            ScrollVertical(focusedFrame.Bottom - visibleFrame.Bottom);
        }
    }

    private void TrackFocus(View view)
    {
        view.MouseEvent += (_, mouse) => HandleMouseWheel(mouse);
        view.HasFocusChanged += (_, _) =>
        {
            if (view.HasFocus)
            {
                EnsureFocusedViewIsVisible();
            }
        };

        foreach (View subView in view.SubViews)
        {
            TrackFocus(subView);
        }
    }

    private void HandleMouseWheel(Mouse mouse)
    {
        int rows = mouse.Flags switch
        {
            var flags when flags.HasFlag(MouseFlags.WheeledUp) => -MouseWheelRows,
            var flags when flags.HasFlag(MouseFlags.WheeledDown) => MouseWheelRows,
            _ => 0,
        };

        if (rows != 0 && ScrollVertical(rows) == true)
        {
            mouse.Handled = true;
        }
    }
}
