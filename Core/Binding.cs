using System.Drawing;
using System.Windows.Forms;

namespace AetherSequence.Core;

internal enum MouseButton
{
    None,
    Left,
    Right,
    Middle,
    X1,
    X2,
}

internal enum InputAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Dash,
    Cast1,
    Cast2,
    Cast3,
    Cast4,
    Cast5,
    Cast6,
    Fire,
    QuickCast,
    ClearCombo,
    Pause,
    Options,
    Confirm,
    NextRune,
    PrevRune,
}

internal struct InputBind : IEquatable<InputBind>
{
    public Keys Key;
    public MouseButton Button;

    public InputBind(Keys key)
    {
        Key = key;
        Button = MouseButton.None;
    }

    public InputBind(MouseButton button)
    {
        Key = Keys.None;
        Button = button;
    }

    public static implicit operator InputBind(Keys key) => new(key);

    public static implicit operator InputBind(MouseButton button) => new(button);

    public readonly bool IsMouse => Button != MouseButton.None;

    public readonly bool IsEmpty => Key == Keys.None && Button == MouseButton.None;

    public bool Equals(InputBind other) => Key == other.Key && Button == other.Button;

    public override bool Equals(object? obj) => obj is InputBind other && Equals(other);

    public override int GetHashCode() => ((int)Key * 397) ^ (int)Button;

    public string Label()
    {
        if (IsMouse) return MouseLabel(Button);
        return KeyLabel(Key);
    }

    public static string MouseLabel(MouseButton button) => button switch
    {
        MouseButton.Left => "ЛКМ",
        MouseButton.Right => "ПКМ",
        MouseButton.Middle => "СКМ",
        MouseButton.X1 => "КМ4",
        MouseButton.X2 => "КМ5",
        _ => "-",
    };

    public static string KeyLabel(Keys key) => key switch
    {
        Keys.None => "-",
        Keys.Space => "ПРОБЕЛ",
        Keys.Enter => "ENTER",
        Keys.Escape => "ESC",
        Keys.Back => "BACKSPACE",
        Keys.Tab => "TAB",
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => "SHIFT",
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => "CTRL",
        Keys.Alt or Keys.Menu => "ALT",
        Keys.Left => "СТРЕЛКА Л",
        Keys.Right => "СТРЕЛКА П",
        Keys.Up => "СТРЕЛКА В",
        Keys.Down => "СТРЕЛКА Н",
        Keys.Oemplus or Keys.Add => "+",
        Keys.OemMinus or Keys.Subtract => "-",
        _ => key.ToString().ToUpperInvariant(),
    };
}
