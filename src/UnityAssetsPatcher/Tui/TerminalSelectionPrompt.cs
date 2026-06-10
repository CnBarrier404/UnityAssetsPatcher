using Spectre.Console;

namespace UnityAssetsPatcher.Tui;

public sealed class TerminalSelectionPrompt
{
    private readonly IAnsiConsole _console;

    public TerminalSelectionPrompt(IAnsiConsole console)
    {
        _console = console;
    }

    public int? ReadSelection(
        int optionCount,
        int initialSelectedIndex,
        Action<int, bool> render,
        ConsoleKey acceptKey = ConsoleKey.Enter)
    {
        ArgumentNullException.ThrowIfNull(render);

        if (optionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(optionCount), "Option count must be greater than 0.");
        }

        if (initialSelectedIndex < 0 || initialSelectedIndex >= optionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialSelectedIndex),
                "Initial selected index must reference an available option.");
        }

        int selectedIndex = initialSelectedIndex;
        bool clear = true;

        _console.Cursor.Show(false);

        while (true)
        {
            render(selectedIndex, clear);
            clear = false;

            var maybeKey = _console.Input.ReadKey(intercept: true);

            if (maybeKey is null)
            {
                return null;
            }

            ConsoleKeyInfo key = maybeKey.Value;

            if (key.Key == acceptKey)
            {
                return selectedIndex;
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    return null;
                case ConsoleKey.UpArrow:
                    selectedIndex = selectedIndex == 0 ? optionCount - 1 : selectedIndex - 1;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = selectedIndex == optionCount - 1 ? 0 : selectedIndex + 1;
                    break;
            }
        }
    }
}
