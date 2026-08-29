using Terminal.Gui.ViewBase;

namespace UnityAssetsPatcher.TUI.Framework;

internal static class ViewExtensions
{
    public static void RemoveAllAndDispose(this View parent, params View[] retainedChildren)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(retainedChildren);

        foreach (View child in parent.RemoveAll())
        {
            if (!retainedChildren.Any(retained => ReferenceEquals(retained, child)))
            {
                child.Dispose();
            }
        }
    }
}
