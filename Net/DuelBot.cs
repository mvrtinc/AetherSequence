using System.Numerics;
using AetherSequence.Core;

namespace AetherSequence.Net;

/// <summary>
/// Бот-соперник для дуэли. Живёт прямо на хосте: не подключается по сети,
/// а просто подставляет свой ввод в слот гостя. Нужен, чтобы можно было
/// проверить бой в одиночку, без второго компьютера.
/// </summary>
internal sealed class DuelBot
{
    private readonly Rng _rng;
    private int _frame;
    private float _dashEvery;
    private float _burst;
    private Vector2 _lastPos;

    public string Nick = "БОТ";
    public float Skill = 0.7f;

    public DuelBot(uint seed, string nick, float skill = 0.7f)
    {
        _rng = new Rng(seed == 0 ? 0xB07u : seed);
        Nick = nick;
        Skill = GameMath.Clamp(skill, 0.1f, 1f);
        _dashEvery = 90f + _rng.Range(0f, 90f);
        _burst = _rng.Range(0f, 40f);
    }

    /// <summary>
    /// Считает ввод бота на один кадр. <paramref name="me"/> - позиция бота,
    /// <paramref name="target"/> - соперник.
    /// </summary>
    public RemoteInputState Think(Vector2 me, Vector2 target, Vector2 view, int frame)
    {
        _frame++;
        _lastPos = me;

        Vector2 delta = target - me;
        float dist = delta.Length();
        Vector2 dir = dist > 0.01f ? delta / dist : new Vector2(1f, 0f);

        float radius = 60f * Skill + 26f;

        // Держим дистанцию: близко - отходим, далеко - идём в лоб,
        // в промежутке - кружим, чтобы бой не превращался в стену.
        Vector2 move;
        if (dist > radius + 55f) move = dir;
        else if (dist < radius) move = -dir;
        else move = new Vector2(-dir.Y, dir.X) * (_frame % 240 < 120 ? 1f : -1f);

        uint down = 0u;
        if (move.X > 0.25f) down |= 1u << (int)InputAction.MoveRight;
        if (move.X < -0.25f) down |= 1u << (int)InputAction.MoveLeft;
        if (move.Y > 0.25f) down |= 1u << (int)InputAction.MoveDown;
        if (move.Y < -0.25f) down |= 1u << (int)InputAction.MoveUp;

        // Рывок - редко, чтобы не улетать с карты.
        if (dist > radius + 40f && _frame > 120 && _frame % (int)_dashEvery == 0)
        {
            down |= 1u << (int)InputAction.Dash;
            _dashEvery = 110f + _rng.Range(0f, 130f);
        }

        // Каст: чем точнее цель, тем чаще стреляет.
        float aimError = 1f - Skill * 0.55f;
        float gate = MathF.Max(8f, 46f * aimError);
        bool shoot = dist < 300f && (_frame % (int)gate) == 0;
        if (shoot) down |= 1u << (int)InputAction.Fire;

        // Иногда меняем руны, чтобы появлялись разные заклинания.
        // Cast1..Cast6 - это биты 5..10 в RemoteInputState.Down.
        uint pressed = 0u;
        _burst -= 1f;
        if (_burst <= 0f)
        {
            _burst = 60f + _rng.Range(0f, 120f);
            int rune = (int)_rng.Range(1f, 7f);
            down |= 1u << ((int)InputAction.Cast1 + rune - 1);
        }

        return new RemoteInputState
        {
            Down = down,
            Pressed = pressed,
            MouseX = view.X * 0.5f + dir.X * 40f,
            MouseY = view.Y * 0.5f + dir.Y * 40f,
            Any = true,
        };
    }
}