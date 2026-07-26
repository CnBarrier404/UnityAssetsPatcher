using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class WorkingIndicator : View
{
    private readonly SpinnerView _spinner;
    private readonly Label _label;
    private bool _spinning;

    public WorkingIndicator(string message = "", TextRole role = TextRole.Preview)
    {
        Width = Dim.Fill();
        Height = 1;
        SetScheme(TerminalTheme.GetTextScheme(role));

        _spinner = new SpinnerView
        {
            X = 0,
            Y = 0,
            Style = new SpinnerStyle.Dots(),
            Visible = false,
        };

        _label = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
        };

        Add(_spinner, _label);

        Initialized += (_, _) =>
        {
            if (_spinning)
            {
                _spinner.AutoSpin = true;
            }
        };

        if (message.Length > 0)
        {
            Spin(message);
        }
    }

    public void Spin(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _spinning = true;
        _spinner.Visible = true;
        _label.X = 2;
        _label.Text = message;

        if (IsInitialized)
        {
            _spinner.AutoSpin = true;
        }
    }

    public void Still(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _spinning = false;
        _spinner.AutoSpin = false;
        _spinner.Visible = false;
        _label.X = 0;
        _label.Text = text;
    }
}
