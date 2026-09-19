using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;

namespace UnityAssetsPatcher.Notifications;

// The panel occupies the host's height so bottom-anchored coordinates remain stable
// when items enter and leave. Transforms animate positions without animating layout.
public sealed class NotificationPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;
        double itemHeight = double.IsFinite(availableSize.Height)
            ? Math.Max(0, (availableSize.Height - NotificationMotion.Spacing * Math.Max(0, Children.Count - 1)) /
                          Math.Max(1, Children.Count))
            : double.PositiveInfinity;

        foreach (Control child in Children)
        {
            child.Measure(new Size(availableSize.Width, itemHeight));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
        }

        height += NotificationMotion.Spacing * Math.Max(0, Children.Count - 1);
        return new Size(width, double.IsFinite(availableSize.Height) ? availableSize.Height : height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double bottom = finalSize.Height;
        for (int index = Children.Count - 1; index >= 0; index--)
        {
            Control child = Children[index];
            double height = child.DesiredSize.Height;
            bottom -= height;
            child.Arrange(new Rect(0, 0, finalSize.Width, height));

            if (child.RenderTransform is not TranslateTransform position)
            {
                position = new TranslateTransform(0, bottom);
                child.RenderTransform = position;
            }

            if (position.Transitions is null)
            {
                position.Transitions =
                [
                    new DoubleTransition
                    {
                        Property = TranslateTransform.YProperty,
                        Duration = NotificationMotion.RepositionDuration,
                        Easing = NotificationMotion.RepositionEasing()
                    }
                ];
            }

            position.Y = bottom;
            bottom -= NotificationMotion.Spacing;
        }

        return finalSize;
    }
}
