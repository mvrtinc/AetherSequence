using System.Numerics;
using AetherSequence.Core;

namespace AetherSequence.Net;

/// <summary>
/// Ввод, приходящий по сети. Хост использует его для второго игрока:
/// те же действия, что и у локального Input, но из пришедшего пакета.
/// Реализует тот же публичный интерфейс выбора, поэтому Player-код
/// не нужно дублировать - достаточно передать другой источник.
/// </summary>
internal sealed class RemoteInput
{
    private uint _down;
    private uint _pressed;
    private uint _released;
    private Vector2 _move;
    private float _mouseX = 320f;
    private float _mouseY = 180f;
    private float _fireHold;
    private bool _wasFireDown;

    public uint Down => _down;
    public uint Pressed => _pressed;

    public Vector2 Move => _move;
    public float MouseX => _mouseX;
    public float MouseY => _mouseY;
    public bool Clicked => Has(_pressed, InputAction.Confirm);
    public float HoldFire => _fireHold;

    public void Apply(RemoteInputState state)
    {
        _down = state.Down;
        _pressed = state.Pressed;
        _released = state.Down;
        _mouseX = state.MouseX;
        _mouseY = state.MouseY;

        Vector2 move = Vector2.Zero;
        if (Has(_down, InputAction.MoveLeft)) move.X -= 1f;
        if (Has(_down, InputAction.MoveRight)) move.X += 1f;
        if (Has(_down, InputAction.MoveUp)) move.Y -= 1f;
        if (Has(_down, InputAction.MoveDown)) move.Y += 1f;
        _move = GameMath.ClampLength(move, 1f);

        bool fireDown = Has(_down, InputAction.Fire);
        _fireHold = fireDown ? _fireHold + 1f / 60f : 0f;
        _wasFireDown = fireDown;
    }

    public void Reset()
    {
        _down = 0u;
        _pressed = 0u;
        _released = 0u;
        _move = Vector2.Zero;
        _fireHold = 0f;
    }

    public bool Has(uint mask, InputAction action) => (mask & (1u << (int)action)) != 0u;

    public bool IsDown(InputAction action) => Has(_down, action);

    public bool IsPressed(InputAction action) => Has(_pressed, action);

    public bool IsReleased(InputAction action) => Has(_released, action) && !Has(_down, action);
}

/// <summary>
/// Обёртка сетевого состояния ввода в контракт IPlayerInput, который ожидает Player.
/// </summary>
internal readonly struct RemoteInputView : IPlayerInput
{
    private readonly RemoteInputState _state;
    private readonly float _fireHold;

    public RemoteInputView(RemoteInputState state)
    {
        _state = state;
        _fireHold = 0f;
    }

    public Vector2 Move
    {
        get
        {
            Vector2 move = Vector2.Zero;
            if (Has(InputAction.MoveLeft)) move.X -= 1f;
            if (Has(InputAction.MoveRight)) move.X += 1f;
            if (Has(InputAction.MoveUp)) move.Y -= 1f;
            if (Has(InputAction.MoveDown)) move.Y += 1f;
            return GameMath.ClampLength(move, 1f);
        }
    }

    public float MouseX => _state.MouseX;

    public float MouseY => _state.MouseY;

    public bool Down(InputAction action) => Has(action);

    public bool Pressed(InputAction action) => (_state.Pressed & (1u << (int)action)) != 0u;

    public float HoldTime(InputAction action) => _fireHold;

    private bool Has(InputAction action) => (_state.Down & (1u << (int)action)) != 0u;
}
