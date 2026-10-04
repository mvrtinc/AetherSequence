using System.Drawing;
using System.Numerics;
using System.Windows.Forms;

namespace AetherSequence.Core;

internal sealed class Input : IPlayerInput
{
    private readonly Settings _settings;
    private readonly HashSet<Keys> _keysDown = new();
    private readonly HashSet<Keys> _keysPressed = new();
    private readonly HashSet<Keys> _keysReleased = new();
    private readonly HashSet<MouseButton> _mouseDown = new();
    private readonly HashSet<MouseButton> _mousePressed = new();
    private readonly HashSet<MouseButton> _mouseReleased = new();
    private readonly Dictionary<InputBind, float> _hold = new();

    public Input(Settings settings)
    {
        _settings = settings;
    }

    public Vector2 Move { get; private set; }

    public PointF Mouse { get; private set; }

    public bool MouseInside { get; private set; }

    public int WheelDelta { get; private set; }

    public bool Clicked => _mousePressed.Contains(MouseButton.Left);

    public Keys LastKeyDown { get; private set; } = Keys.None;

    /// <summary>Клавиши, нажатые в этом кадре (для текстовых полей).</summary>
    public List<Keys> PressedKeys { get; } = new();

    /// <summary>Символ по нажатой клавише с учётом Shift, либо '\0'.</summary>
    public static char TranslateChar(Keys key)
    {
        if (key is Keys.Space) return ' ';
        int code = (int)key;
        if (code is >= (int)Keys.D0 and <= (int)Keys.D9)
        {
            int digit = code - (int)Keys.D0;
            return (char)('0' + digit);
        }
        if (code is >= (int)Keys.NumPad0 and <= (int)Keys.NumPad9)
        {
            int digit = code - (int)Keys.NumPad0;
            return (char)('0' + digit);
        }
        if (key is >= Keys.A and <= Keys.Z)
        {
            char letter = (char)('A' + ((int)key - (int)Keys.A));
            return letter;
        }
        // Точка нужна для ввода IP-адреса. На разных раскладках она приходит
        // либо как OemPeriod, либо как Oem2 (русская "." Shift+7).
        if (key is Keys.OemPeriod or Keys.Oem2 or Keys.Decimal) return '.';
        return '\0';
    }

    public void Tick(float dt)
    {
        Vector2 move = Vector2.Zero;
        if (Down(InputAction.MoveLeft)) move.X -= 1f;
        if (Down(InputAction.MoveRight)) move.X += 1f;
        if (Down(InputAction.MoveUp)) move.Y -= 1f;
        if (Down(InputAction.MoveDown)) move.Y += 1f;
        Move = GameMath.ClampLength(move, 1f);

        if (_hold.Count > 0)
        {
            List<InputBind>? expired = null;
            foreach ((InputBind InputBind, float time) in _hold)
            {
                if (IsDown(InputBind))
                {
                    _hold[InputBind] = time + dt;
                }
                else
                {
                    expired ??= new List<InputBind>();
                    expired.Add(InputBind);
                }
            }
            if (expired is not null)
            {
                foreach (InputBind InputBind in expired) _hold.Remove(InputBind);
            }
        }
    }

    public bool Down(InputAction action)
    {
        foreach (InputBind InputBind in _settings.Binding(action))
        {
            if (IsDown(InputBind)) return true;
        }
        return false;
    }

    public bool Pressed(InputAction action)
    {
        foreach (InputBind InputBind in _settings.Binding(action))
        {
            if (IsPressed(InputBind)) return true;
        }
        return false;
    }

    public bool Released(InputAction action)
    {
        foreach (InputBind InputBind in _settings.Binding(action))
        {
            if (IsReleased(InputBind)) return true;
        }
        return false;
    }

    public float HoldTime(InputAction action)
    {
        float best = 0f;
        foreach (InputBind InputBind in _settings.Binding(action))
        {
            if (_hold.TryGetValue(InputBind, out float time) && time > best) best = time;
        }
        return best;
    }

    public bool IsDown(InputBind InputBind) => InputBind.IsMouse ? _mouseDown.Contains(InputBind.Button) : _keysDown.Contains(InputBind.Key);

    public bool IsPressed(InputBind InputBind) => InputBind.IsMouse ? _mousePressed.Contains(InputBind.Button) : _keysPressed.Contains(InputBind.Key);

    public bool IsReleased(InputBind InputBind) => InputBind.IsMouse ? _mouseReleased.Contains(InputBind.Button) : _keysReleased.Contains(InputBind.Key);

    public bool RawDown(Keys key) => _keysDown.Contains(key);

    public bool RawPressed(Keys key) => _keysPressed.Contains(key);

    public bool AnyMousePressed()
    {
        return _mousePressed.Contains(MouseButton.Left)
            || _mousePressed.Contains(MouseButton.Right)
            || _mousePressed.Contains(MouseButton.Middle)
            || _mousePressed.Contains(MouseButton.X1)
            || _mousePressed.Contains(MouseButton.X2);
    }

    public bool TryCaptureBinding(out InputBind captured)
    {
        if (_keysPressed.Count > 0)
        {
            foreach (Keys key in _keysPressed)
            {
                if (key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LShiftKey or Keys.RShiftKey) continue;
                captured = new InputBind(key);
                return true;
            }
        }

        foreach (MouseButton button in new[] { MouseButton.Left, MouseButton.Right, MouseButton.Middle, MouseButton.X1, MouseButton.X2 })
        {
            if (_mousePressed.Contains(button))
            {
                captured = new InputBind(button);
                return true;
            }
        }

        captured = new InputBind(Keys.None);
        return false;
    }

    public void KeyDown(Keys key)
    {
        if (key == Keys.None) return;
        LastKeyDown = key;
        if (!_keysDown.Contains(key))
        {
            _keysPressed.Add(key);
            _hold[new InputBind(key)] = 0f;
        }
        _keysDown.Add(key);
    }

    public void KeyUp(Keys key)
    {
        if (key == Keys.None) return;
        _keysDown.Remove(key);
        _keysReleased.Add(key);
    }

    public void MouseDown(MouseButton button)
    {
        if (!_mouseDown.Contains(button))
        {
            _mousePressed.Add(button);
            _hold[new InputBind(button)] = 0f;
        }
        _mouseDown.Add(button);
    }

    public void MouseUp(MouseButton button)
    {
        _mouseDown.Remove(button);
        _mouseReleased.Add(button);
    }

    public void MouseMove(PointF virtualPosition)
    {
        Mouse = virtualPosition;
        MouseInside = true;
    }

    public void MouseLeave()
    {
        MouseInside = false;
    }

    public void MouseWheel(int delta) => WheelDelta += delta;

    public void ClearAll()
    {
        _keysDown.Clear();
        _keysPressed.Clear();
        _keysReleased.Clear();
        _mouseDown.Clear();
        _mousePressed.Clear();
        _mouseReleased.Clear();
        _hold.Clear();
        Move = Vector2.Zero;
    }

    public void EndFrame()
    {
        PressedKeys.Clear();
        foreach (Keys key in _keysPressed) PressedKeys.Add(key);
        _keysPressed.Clear();
        _keysReleased.Clear();
        _mousePressed.Clear();
        _mouseReleased.Clear();
        WheelDelta = 0;
    }
}
