using System.Drawing;
using System.Numerics;
using AetherSequence.Art;
using AetherSequence.Core;

namespace AetherSequence.Entities;

internal enum PickupKind
{
    Xp,
    Mana,
    Health,
}

internal sealed class Pickup
{
    public Vector2 Pos;
    public Vector2 Vel;
    public PickupKind Kind;
    public float Value;
    public float Life = 16f;
    public float Age;
    public bool Dead;
    public float Wave;

    public void Update(float dt, Vector2 target, float magnetRange)
    {
        Age += dt;
        Life -= dt;
        if (Life <= 0f)
        {
            Dead = true;
            return;
        }
        Vector2 to = target - Pos;
        float dist = to.Length();
        if (dist < magnetRange)
        {
            Vel += GameMath.Normalized(to) * (420f * dt);
            Vel = GameMath.ClampLength(Vel, 210f);
        }
        else
        {
            Vel *= MathF.Max(0f, 1f - 5f * dt);
        }
        Pos += Vel * dt;
    }

    public void Draw(Graphics g, float time)
    {
        float pulse = 1f + 0.18f * MathF.Sin(time * 5f + Wave);
        float alpha = Life < 3f ? (MathF.Sin(Life * 18f) > 0f ? 0.35f : 1f) : 1f;
        Color c = Kind switch
        {
            PickupKind.Xp => Palette.Xp,
            PickupKind.Mana => Palette.Mana,
            _ => Palette.Health,
        };
        Palette.AddGlow(g, Pos, 5.5f * pulse, c, 0.55f * alpha);
        float s = 2f * pulse;
        Palette.Fill(g, Palette.Fade(c, alpha), Pos.X - s * 0.5f, Pos.Y - s * 0.5f, s, s);
        Palette.Fill(g, Palette.Fade(Color.White, alpha), Pos.X - 0.5f, Pos.Y - 0.5f, 1f, 1f);
    }
}
