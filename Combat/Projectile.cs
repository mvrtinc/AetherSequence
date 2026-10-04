using System.Drawing;
using System.Numerics;
using AetherSequence.Art;
using AetherSequence.Core;

namespace AetherSequence.Combat;

internal enum ProjectileStyle
{
    Orb,
    Bolt,
    Shard,
    Rock,
    Wave,
    Star,
    Vortex,
}

internal sealed class Projectile
{
    public Vector2 Pos;
    public Vector2 Vel;
    public Vector2 Aim;
    public float Radius = 3f;
    public float Size = 3f;
    public float Length = 9f;
    public float Damage;
    public Element Element = Element.Fire;
    public Color Color = Color.White;
    public bool Hostile;
    public int Pierce;
    public float Life = 2f;
    public float Age;
    public float Slow;
    public float Knockback;
    public float Stun;
    public bool Explode;
    public float ExplodeRadius = 28f;
    public float Homing;
    public float TrailTimer;
    public ProjectileStyle Style = ProjectileStyle.Orb;
    public bool Stretch;
    public bool Dead;
    public float Score = 1f;
    public float Wave;

    /// <summary>Кто выпустил снаряд: 0 - хост, 1 - гость, -1 - враг.</summary>
    public int Owner = -1;

    public readonly HashSet<int> Hits = new();

    public float Speed => Vel.Length();

    public void Draw(Graphics g, float time)
    {
        float pulse = 0.85f + 0.15f * MathF.Sin(time * 18f + Wave);
        Color core = GameMath.Mix(Color.White, Color, 0.25f);

        switch (Style)
        {
            case ProjectileStyle.Rock:
                Palette.AddGlow(g, Pos, Size * 1.7f * pulse, Color, 0.45f);
                Palette.Fill(g, GameMath.Shade(Color, 0.7f), Pos.X - Size * 0.6f, Pos.Y - Size * 0.6f, Size * 1.2f, Size * 1.2f);
                Palette.Fill(g, core, Pos.X - Size * 0.4f, Pos.Y - Size * 0.4f, Size * 0.8f, Size * 0.8f);
                break;

            case ProjectileStyle.Star:
            {
                Palette.AddGlow(g, Pos, Size * 2.8f * pulse, Color, 0.85f);
                float arm = Size * 1.5f;
                float t = 1f;
                Palette.Fill(g, core, Pos.X - arm, Pos.Y - t * 0.5f, arm * 2f, t);
                Palette.Fill(g, core, Pos.X - t * 0.5f, Pos.Y - arm, t, arm * 2f);
                Palette.Fill(g, Color.White, Pos.X - 1f, Pos.Y - 1f, 2f, 2f);
                break;
            }

            case ProjectileStyle.Vortex:
            {
                Palette.AddGlow(g, Pos, Size * 2.4f * pulse, Color, 0.7f);
                float r = Size * (1.1f + 0.25f * MathF.Sin(time * 12f + Wave));
                Palette.Circle(g, Palette.Fade(Color, 0.9f), Pos.X, Pos.Y, r);
                Palette.Circle(g, Palette.Fade(Color.White, 0.6f), Pos.X, Pos.Y, r * 0.45f);
                break;
            }

            case ProjectileStyle.Shard:
            {
                Vector2 dir = GameMath.Normalized(Vel);
                Vector2 side = new(-dir.Y, dir.X);
                DrawQuad(g, Pos, side, Size * 2.6f, Size * 0.7f, core);
                DrawQuad(g, Pos, side, Size * 1.2f, Size * 0.3f, Color.White);
                Palette.AddGlow(g, Pos, Size * 1.8f, Color, 0.5f);
                break;
            }

            default:
            {
                float glow = Size * 2.6f * pulse;
                Palette.AddGlow(g, Pos, glow, Color, 0.75f);
                Palette.AddGlow(g, Pos, glow * 0.45f, Color.White, 0.5f);
                if (Stretch)
                {
                    Vector2 dir = GameMath.Normalized(Vel);
                    Vector2 side = new(-dir.Y, dir.X);
                    DrawQuad(g, Pos, side, Length, Size * 0.8f, core);
                    DrawQuad(g, Pos, side, Length * 0.5f, Size * 0.45f, Color.White);
                }
                else
                {
                    float s = Size * pulse;
                    Palette.Fill(g, core, Pos.X - s * 0.5f, Pos.Y - s * 0.5f, s, s);
                    Palette.Fill(g, Color.White, Pos.X - s * 0.22f, Pos.Y - s * 0.22f, s * 0.44f, s * 0.44f);
                }
                break;
            }
        }
    }

    private static void DrawQuad(Graphics g, Vector2 center, Vector2 side, float length, float width, Color color)
    {
        Vector2 dir = side * (width * 0.5f);
        Vector2 along = new Vector2(side.Y * length * 0.5f, -side.X * length * 0.5f);
        PointF[] pts =
        {
            Palette.ToPointF(center - along - dir),
            Palette.ToPointF(center + along - dir),
            Palette.ToPointF(center + along + dir),
            Palette.ToPointF(center - along + dir),
        };
        g.FillPolygon(Palette.Brush(color), pts);
    }
}
