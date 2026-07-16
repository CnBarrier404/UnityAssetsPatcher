using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class ToggleItem : View
{
    public event EventHandler? IsSelectedChanged;

    public string Name { get; }
    public ActionButton Button { get; }
    public StyledLabel Description { get; }

    public bool IsSelected
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;

            UpdateText();
            IsSelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ToggleItem(string name, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Width = Dim.Fill();
        Height = 2;
        CanFocus = true;

        Button = new ActionButton(string.Empty)
        {
            X = 0, Y = 0, Width = Dim.Fill()
        };

        Description = new StyledLabel(description ?? string.Empty, TextRole.Muted)
        {
            X = 6,
            Y = 1,
            Width = Dim.Fill(),
        };
        Button.KeyDown += (_, key) =>
        {
            if (key != Key.Space) return;
            key.Handled = true;
            IsSelected = !IsSelected;
        };

        Button.Accepted += (_, _) => { IsSelected = !IsSelected; };

        Button.HasFocusChanged += (_, _) => { UpdateText(); };

        Add(Button, Description);

        UpdateText();
    }

    private void UpdateText()
    {
        string indicator = Button.HasFocus ? ">" : " ";
        string checkbox = IsSelected ? "[*]" : "[ ]";
        Button.Text = $"{indicator} {checkbox} {Name}";
    }
}
