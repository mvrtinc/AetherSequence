using System.Drawing;
using System.Numerics;
using AetherSequence.Art;
using AetherSequence.Core;

namespace AetherSequence.Combat;

internal struct Particle
{
    public Vector2 Pos;
    public Vector2 Vel;
    public float Life;
    public float MaxLife;
    public float Size;
    public float Drag;
    public float Gravity;
    public Color Color;
    public bool Additive;
}

internal struct FloatingText
{
    public Vector2 Pos;
    public Vector2 Vel;
    public string Value;
    public Color Color;
    public float Life;
    public float MaxLife;
    public bool Big;
}

internal struct MagicRing
{
    public Vector2 Pos;
    public float Radius;
    public float MaxRadius;
    public float Life;
    public float MaxLife;
    public float Thickness;
    public Color Color;
    public bool Filled;
}

internal struct Telegraph
{
    public Vector2 Pos;
    public float Radius;
    public float Timer;
    public float MaxTimer;
    public Color Color;
    public float Damage;
    public float Knockback;
    public float Stun;
    public bool Line;
    public Vector2 Dir;
    public float Length;
    public float Width;

    /// <summary>
    /// Анимация удара. null - значит это телеграф врага, а не дождь игрока.
    /// Инициализатора нет намеренно: иначе struct требует явный конструктор,
    /// а поле всё равно всегда задаётся при создании телеграфа.
    /// </summary>
    public string? Sheet;
}

internal sealed class MagicField
{
    public Vector2 Pos;
    public Vector2 Drift;    public float Radius;
    public float Life;
    public float MaxLife;
    public float Dps;
    public float Pull;
    public float Slow;
    public float Stun;
    public float Knockback;
    public Color Color;
    public Element Element;
    public float Tick;
    public float Angle;
    public float Life01 => MaxLife <= 0f ? 0f : GameMath.Clamp(Life / MaxLife, 0f, 1f);
}

internal struct BeamFx
{
    public Vector2 Start;
    public Vector2 End;
    public Vector2 Perpendicular;
    public Color Color;
    public float Life;
    public float MaxLife;
}

/// <summary>Растровая анимация заклинания, проигрываемая один раз при касте.</summary>
internal sealed class CastSprite
{
    public Vector2 Pos;
    public string Sheet = string.Empty;
    public float Size;
    public float Age;
    public float Life;

    /// <summary>Прогресс анимации 0..1.</summary>
    public float T01 => Life <= 0f ? 1f : GameMath.Clamp01(Age / Life);

    /// <summary>0.78 от размера эффекта - сам кадр местами уже вчетверо больше нужного.</summary>
    public const float SizeScale = 0.78f;
}

internal sealed class EffectSystem
{
    public const int MaxParticles = 1500;

    public readonly List<Particle> Particles = new();
    public readonly List<FloatingText> Texts = new();
    public readonly List<MagicRing> Rings = new();
    public readonly List<Telegraph> Telegraphs = new();
    public readonly List<MagicField> Fields = new();

    public readonly List<BeamFx> Beams = new();
    public readonly List<CastSprite> CastSprites = new();

    public void Clear()
    {
        Particles.Clear();
        Texts.Clear();
        Rings.Clear();
        Telegraphs.Clear();
        Fields.Clear();
        Beams.Clear();
        CastSprites.Clear();
    }

    /// <summary>
    /// Проигрывает растровую анимацию заклинания. Если ассетов нет или
    /// лист неизвестен - эффект не создаётся и игра работает как раньше.
    /// </summary>
    public void CastSpriteAt(Vector2 pos, string sheet, float size, float life = 0.36f)
    {
        if (!SpellSprites.Available) return;
        if (SpellSprites.FrameCount(sheet) <= 0) return;
        if (CastSprites.Count >= 48) return;

        CastSprites.Add(new CastSprite
        {
            Pos = pos,
            Sheet = sheet,
            Size = size * CastSprite.SizeScale,
            Age = 0f,
            Life = life,
        });
    }

    public void BeamFX(Vector2 start, Vector2 end, Vector2 perpendicular, Color color, float life)
    {
        Beams.Add(new BeamFx
        {
            Start = start,
            End = end,
            Perpendicular = perpendicular,
            Color = color,
            Life = life,
            MaxLife = life,
        });
    }

    public void Update(float dt, Game game)
    {
        for (int i = Particles.Count - 1; i >= 0; i--)
        {
            Particle p = Particles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                Particles.RemoveAt(i);
                continue;
            }
            p.Vel *= MathF.Max(0f, 1f - p.Drag * dt);
            p.Vel.Y += p.Gravity * dt;
            p.Pos += p.Vel * dt;
            Particles[i] = p;
        }

        for (int i = Texts.Count - 1; i >= 0; i--)
        {
            FloatingText t = Texts[i];
            t.Life -= dt;
            if (t.Life <= 0f)
            {
                Texts.RemoveAt(i);
                continue;
            }
            t.Pos += t.Vel * dt;
            t.Vel *= MathF.Max(0f, 1f - 2.2f * dt);
            Texts[i] = t;
        }

        for (int i = Rings.Count - 1; i >= 0; i--)
        {
            MagicRing r = Rings[i];
            r.Life -= dt;
            if (r.Life <= 0f)
            {
                Rings.RemoveAt(i);
                continue;
            }
            float t = 1f - GameMath.Clamp(r.Life / r.MaxLife, 0f, 1f);
            r.Radius = GameMath.Lerp(r.MaxRadius * 0.15f, r.MaxRadius, MathF.Sqrt(t));
            Rings[i] = r;
        }

        for (int i = Telegraphs.Count - 1; i >= 0; i--)
        {
            Telegraph t = Telegraphs[i];
            t.Timer -= dt;
            if (t.Timer <= 0f)
            {
                Telegraphs.RemoveAt(i);
                CombatUtil.ResolveTelegraph(game, t);
                continue;
            }
            Telegraphs[i] = t;
        }

        for (int i = Beams.Count - 1; i >= 0; i--)
        {
            BeamFx b = Beams[i];
            b.Life -= dt;
            if (b.Life <= 0f)
            {
                Beams.RemoveAt(i);
                continue;
            }
            Beams[i] = b;
        }

        for (int i = CastSprites.Count - 1; i >= 0; i--)
        {
            CastSprite c = CastSprites[i];
            c.Age += dt;
            if (c.Age >= c.Life)
            {
                CastSprites.RemoveAt(i);
                continue;
            }
            CastSprites[i] = c;
        }

        for (int i = Fields.Count - 1; i >= 0; i--)
        {
            MagicField f = Fields[i];
            f.Life -= dt;
            f.Angle += dt * 6f;
            if (f.Life <= 0f)
            {
                Fields.RemoveAt(i);
                continue;
            }
            if (f.Drift.LengthSquared() > 0.01f)
            {
                Vector2 next = game.Level.Move(f.Pos, f.Drift * dt, f.Radius * 0.55f);
                if (Vector2.Distance(next, f.Pos) < 0.05f) f.Drift = Vector2.Zero;
                f.Pos = next;
            }
            f.Tick -= dt;
            if (f.Tick <= 0f)
            {
                f.Tick = 0.3f;
                CombatUtil.FieldTick(game, f);
            }
            Fields[i] = f;
        }
    }

    public void Draw(Graphics g, float time)
    {
        // Растровые анимации каста идут под процедурными кольцами, чтобы
        // не перекрывать читаемые телеграфы.
        foreach (CastSprite c in CastSprites)
        {
            SpellSprites.Draw(g, c.Sheet, c.Pos, c.Size, c.T01);
        }

        foreach (MagicRing r in Rings)
        {
            float a = GameMath.Clamp(r.Life / r.MaxLife, 0f, 1f);
            Color c = Palette.Fade(r.Color, a * 0.9f);
            float ry = r.Radius * 0.9f;
            if (r.Filled) Palette.Disc(g, Palette.Fade(r.Color, a * 0.28f), r.Pos.X, r.Pos.Y, r.Radius);
            Palette.Circle(g, c, r.Pos.X, r.Pos.Y, r.Radius);
            Palette.Circle(g, c, r.Pos.X, r.Pos.Y, ry);
            Palette.AddGlowEllipse(g, r.Pos, new Vector2(r.Radius, ry), r.Color, a * 0.55f);
        }

        foreach (Telegraph t in Telegraphs)
        {
            float p = 1f - GameMath.Clamp(t.Timer / t.MaxTimer, 0f, 1f);
            float a = 0.25f + 0.45f * p;
            if (t.Line)
            {
                Vector2 dir = t.Dir;
                float w = t.Width * (0.3f + p * 0.7f);
                Palette.Fill(g, Palette.Fade(t.Color, a * 0.35f), t.Pos.X, t.Pos.Y - w * 0.5f, t.Length, w);
                Palette.Fill(g, Palette.Fade(t.Color, 0.9f), t.Pos.X, t.Pos.Y - 0.5f, t.Length * p, 1f);
            }
            else
            {
                Palette.AddGlowEllipse(g, t.Pos, new Vector2(t.Radius, t.Radius), t.Color, a * 0.5f);
                Palette.Circle(g, Palette.Fade(t.Color, 0.5f), t.Pos.X, t.Pos.Y, t.Radius);
                float r = t.Radius * (0.3f + p * 0.7f);
                Palette.Circle(g, Palette.Fade(t.Color, 0.95f), t.Pos.X, t.Pos.Y, r);
            }
        }

        foreach (BeamFx b in Beams)
        {
            float t = GameMath.Clamp01(b.Life / b.MaxLife);
            float w = b.Perpendicular.Length() * (0.35f + t * 0.65f);
            Vector2 p = GameMath.Normalized(b.Perpendicular) * w;
            g.FillPolygon(Palette.Brush(Palette.Fade(b.Color, t * 0.85f)), new[]
            {
                Palette.ToPointF(b.Start - p), Palette.ToPointF(b.End - p),
                Palette.ToPointF(b.End + p), Palette.ToPointF(b.Start + p),
            });
            Vector2 p2 = GameMath.Normalized(b.Perpendicular) * (w * 0.3f);
            g.FillPolygon(Palette.Brush(Palette.Fade(Color.White, t * 0.85f)), new[]
            {
                Palette.ToPointF(b.Start - p2), Palette.ToPointF(b.End - p2),
                Palette.ToPointF(b.End + p2), Palette.ToPointF(b.Start + p2),
            });
            for (int i = 0; i <= 8; i++)
            {
                Palette.AddGlow(g, Vector2.Lerp(b.Start, b.End, i / 8f), w * 1.5f, b.Color, t * 0.45f);
            }
        }

        foreach (MagicField f in Fields)
        {
            float a = GameMath.Clamp(f.Life01 * 1.6f, 0f, 1f);
            float r = f.Radius * (0.85f + 0.15f * MathF.Sin(time * 4f + f.Angle));
            Palette.AddGlowEllipse(g, f.Pos, new Vector2(r, r), f.Color, a * 0.5f);
            Palette.Circle(g, Palette.Fade(f.Color, a * 0.85f), f.Pos.X, f.Pos.Y, r);
            Palette.Circle(g, Palette.Fade(f.Color, a * 0.4f), f.Pos.X, f.Pos.Y, r * 0.62f);

            float arm = f.Radius * 0.62f;
            for (int i = 0; i < 3; i++)
            {
                float ang = f.Angle + i * GameMath.Tau / 3f;
                Vector2 p = f.Pos + GameMath.FromAngle(ang) * arm;
                Palette.AddGlow(g, p, 3.5f, f.Color, a * 0.8f);
                Palette.Fill(g, Palette.Fade(Color.White, a), p.X - 1f, p.Y - 1f, 2f, 2f);
            }
        }

        foreach (Particle p in Particles)
        {
            if (p.Additive) continue;
            float a = GameMath.Clamp(p.Life / p.MaxLife, 0f, 1f);
            float s = p.Size * (0.35f + a * 0.65f);
            Palette.Fill(g, Palette.Fade(p.Color, a * 0.9f), p.Pos.X - s * 0.5f, p.Pos.Y - s * 0.5f, s, s);
        }

        foreach (Particle p in Particles)
        {
            if (!p.Additive) continue;
            float a = GameMath.Clamp(p.Life / p.MaxLife, 0f, 1f);
            float s = p.Size * (0.3f + a * 0.7f);
            Palette.AddGlow(g, p.Pos, s * 1.9f, p.Color, a * 0.85f);
            Palette.Fill(g, Palette.Fade(Color.White, a * 0.8f), p.Pos.X - s * 0.35f, p.Pos.Y - s * 0.35f, s * 0.7f, s * 0.7f);
        }
    }

    public void AddParticle(Particle p)
    {
        if (Particles.Count >= MaxParticles) return;
        Particles.Add(p);
    }

    public void Burst(Vector2 pos, Color color, int count, float speed, float size, float life, bool additive = true, float spread = GameMath.Tau, float angle = 0f)
    {
        for (int i = 0; i < count; i++)
        {
            float a = angle + gameRandom(i, count) * spread;
            float sp = speed * (0.45f + gameRandom(i * 7 + 3, count) * 0.9f);
            AddParticle(new Particle
            {
                Pos = pos,
                Vel = GameMath.FromAngle(a) * sp,
                Life = life * (0.6f + gameRandom(i * 13 + 5, count) * 0.7f),
                MaxLife = life,
                Size = size * (0.6f + gameRandom(i * 3 + 1, count) * 0.8f),
                Drag = 2.2f,
                Color = color,
                Additive = additive,
            });
        }
    }

    private static float gameRandom(int i, int count)
    {
        float x = (i * 0.6180339887f + 0.31f) * (count + 1);
        return GameMath.Clamp(x - MathF.Floor(x), 0f, 1f);
    }

    public void Smoke(Vector2 pos, Color color, int count, float speed, float size, float life)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (i / (float)Math.Max(1, count)) * GameMath.Tau;
            AddParticle(new Particle
            {
                Pos = pos,
                Vel = GameMath.FromAngle(a) * speed * (0.4f + gameRandom(i, count) * 0.8f),
                Life = life * (0.7f + gameRandom(i * 5, count) * 0.6f),
                MaxLife = life,
                Size = size,
                Drag = 1.4f,
                Gravity = -8f,
                Color = color,
                Additive = false,
            });
        }
    }

    public void Ring(Vector2 pos, float maxRadius, Color color, float life, float thickness = 2f, bool filled = false)
    {
        Rings.Add(new MagicRing
        {
            Pos = pos,
            MaxRadius = maxRadius,
            Radius = maxRadius * 0.15f,
            Life = life,
            MaxLife = life,
            Color = color,
            Thickness = thickness,
            Filled = filled,
        });
    }

    public void Label(Vector2 pos, string value, Color color, bool big = false, float life = 0.8f)
    {
        if (Texts.Count > 90) Texts.RemoveAt(0);
        Texts.Add(new FloatingText
        {
            Pos = pos,
            Vel = new Vector2(0f, -26f),
            Value = value,
            Color = color,
            Life = life,
            MaxLife = life,
            Big = big,
        });
    }

    public void Telegraph(Vector2 pos, float radius, float delay, Color color, float damage, float knockback = 0f, float stun = 0f, string sheet = "")
    {
        Telegraphs.Add(new Telegraph
        {
            Pos = pos,
            Radius = radius,
            Timer = delay,
            MaxTimer = delay,
            Color = color,
            Damage = damage,
            Knockback = knockback,
            Stun = stun,
            Sheet = sheet,
        });
    }

    public void LineTelegraph(Vector2 pos, Vector2 dir, float length, float width, float delay, Color color, float damage, float knockback = 0f, float stun = 0f)
    {
        Telegraphs.Add(new Telegraph
        {
            Pos = pos,
            Line = true,
            Dir = GameMath.Normalized(dir),
            Length = length,
            Width = width,
            Timer = delay,
            MaxTimer = delay,
            Color = color,
            Damage = damage,
            Knockback = knockback,
            Stun = stun,
        });
    }

    public MagicField Field(Vector2 pos, SpellDef def, float radius, float duration, float dps, float pull, float slow, float stun, float knockback)
    {
        MagicField f = new()
        {
            Pos = pos,
            Radius = radius,
            Life = duration,
            MaxLife = duration,
            Dps = dps,
            Pull = pull,
            Slow = slow,
            Stun = stun,
            Knockback = knockback,
            Color = def.Color,
            Element = def.Element,
            Tick = 0.35f,
        };
        Fields.Add(f);
        return f;
    }

    public void DrawTexts(Graphics g, Vector2 cam)
    {
        foreach (FloatingText t in Texts)
        {
            float a = GameMath.Clamp(t.Life / t.MaxLife, 0f, 1f);
            float y = t.Pos.Y - cam.Y - (1f - a) * 10f;
            float x = t.Pos.X - cam.X;
            Font font = t.Big ? Text.Medium : Text.Small;
            float w = Text.Width(g, t.Value, font);
            Palette.Fill(g, Palette.Fade(Color.Black, a * 0.55f), x - w * 0.5f - 1f, y - 1f, w + 2f, font.Height);
            Text.DrawCentered(g, t.Value, font, Palette.Fade(t.Color, a), x, y);
        }
    }
}
