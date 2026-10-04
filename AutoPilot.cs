using System.Numerics;
using System.Windows.Forms;
using AetherSequence.Core;

namespace AetherSequence;

internal static class AutoPilot
{
    private static readonly Keys[] Runes = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6 };

    private static Rng _rng = new(0xC0FFEEu);
    private static Keys _move = Keys.None;
    private static Keys _action = Keys.None;
    private static int _wave;
    private static Vector2 _lastPos;
    private static int _stuckFrames;
    private static float _detourSign = 1f;
    private static int _detourFrames;

    public static bool GodMode { get; set; }

    public static void Reset(uint seed)
    {
        _rng = new Rng(seed == 0 ? 0xC0FFEEu : seed);
        _move = Keys.None;
        _action = Keys.None;
        _wave = 0;
        _stuckFrames = 0;
        _detourFrames = 0;
    }

    private static Vector2 Steer(Game game, Vector2 desired)
    {
        if (Vector2.Distance(_lastPos, game.Player.Pos) < 0.6f) _stuckFrames++;
        else _stuckFrames = 0;
        _lastPos = game.Player.Pos;

        if (_stuckFrames > 8 && _detourFrames == 0)
        {
            _detourFrames = 24;
            _detourSign = _rng.Chance(0.5f) ? 1f : -1f;
        }

        if (_detourFrames > 0)
        {
            _detourFrames--;
            if (_detourFrames % 8 == 0) _detourSign = -_detourSign;
            Vector2 side = new(-desired.Y * _detourSign, desired.X * _detourSign);
            if (Vector2.Distance(side, Vector2.Zero) > 0.01f) return side;
        }
        return desired;
    }

    public static void Step(Game game)
    {
        Keys previousMove = _move;
        Keys previousAction = _action;
        _move = Keys.None;
        _action = Keys.None;

        if (game.State == GameState.Playing)
        {
            _wave++;
            if (_wave % 20 == 0)
            {
                Vector2 visible = DevTools.NearestEnemy(game, 1400f);
                Vector2 delta = visible - game.Player.Pos;
                bool hasTarget = Vector2.Distance(visible, game.Player.Pos) > 3f;
                float dist = delta.Length();
                Vector2 move;
                if (hasTarget && dist < 36f) move = -delta;
                else if (hasTarget && dist > 60f) move = delta;
                else if (hasTarget) move = new Vector2(-delta.Y, delta.X) * (_rng.Chance(0.5f) ? 1f : -1f);
                else
                {
                    Vector2 toExit = game.Level.ExitPos - game.Player.Pos;
                    move = toExit.LengthSquared() > 25f ? toExit : new Vector2(_rng.Range(-1f, 1f), _rng.Range(-1f, 1f));
                }
                if (move.LengthSquared() < 0.01f) move = new Vector2(1f, 0f);
                move = Steer(game, move);
                _move = MathF.Abs(move.X) > MathF.Abs(move.Y)
                    ? (move.X > 0 ? Keys.D : Keys.A)
                    : (move.Y > 0 ? Keys.S : Keys.W);
            }
            if (_wave % 13 == 0 && game.Player.Mana >= 25f)
            {
                int slot = _rng.Next(0, 6);
                _action = game.Player.RuneUnlocked[slot] ? Runes[slot] : Keys.None;
            }
            if (_wave % 7 == 0) game.Input.MouseDown(MouseButton.Left);
            else if (_wave % 7 == 3) game.Input.MouseUp(MouseButton.Left);
        }
        else if (game.State == GameState.LevelUp)
        {
            _action = Keys.D1;
        }
        else if (game.State == GameState.Title || game.State == GameState.GameOver || game.State == GameState.Victory)
        {
            _action = game.State == GameState.Title ? Keys.Enter : Keys.R;
        }

        if (previousMove != Keys.None) game.Input.KeyUp(previousMove);
        if (previousAction != Keys.None) game.Input.KeyUp(previousAction);
        if (_move != Keys.None) game.Input.KeyDown(_move);
        if (_action != Keys.None) game.Input.KeyDown(_action);

        if (game.State == GameState.Playing)
        {
            Vector2 aim = DevTools.NearestEnemy(game, 1400f);
            if (Vector2.Distance(aim, game.Player.Pos) > 3f)
            {
                Vector2 screen = aim - game.Camera.Position;
                game.Input.MouseMove(new PointF(screen.X, screen.Y));
            }
        }

        if (GodMode && game.Player.Alive)
        {
            game.Player.Hp = game.Player.MaxHp;
            game.Player.Mana = game.Player.MaxMana;
            game.Player.ManaRegen = 200f;
        }
    }
}
