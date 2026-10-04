using System.Drawing;
using System.Numerics;
using AetherSequence.Art;
using AetherSequence.Audio;
using AetherSequence.Core;
using AetherSequence.Entities;

namespace AetherSequence.Combat;

internal static class CombatUtil
{
    public static void Cast(Game game, Player p, SpellDef def, bool fusion, bool repeated = false)
    {
        game.Caster = p;
        int owner = game.DuelMode
            ? (ReferenceEquals(p, Game.CurrentHostPlayer) ? 0 : 1)
            : -1;

        int cost = fusion ? (int)(def.Cost * 1.2f) : def.Cost;
        if (repeated) cost = (int)(cost * 0.85f);
        float power = 1f;
        if (!p.CanPay(cost))
        {
            power = 0.55f;
        }
        p.Pay(cost);

        float damage = def.Damage * p.DamageMultiplier * power;
        if (fusion) damage *= p.FusionDamageMul;

        p.Flow = MathF.Min(1f, p.Flow + def.FlowGain);
        p.CastFlash = 1f;
        p.ManaRegenDelay = 0.3f;
        p.LastElement = def.Element;
        p.ElementTint = def.Color;
        p.ElementTintTimer = 0.5f;
        p.RememberCast(def, fusion);

        game.Camera.Add(def.Shake * (fusion ? 1.4f : 1f) * power);
        game.Flash(def.Color, fusion ? 0.4f : 0.14f);
        game.Score += def.Score + (fusion ? 3 : 0);
        game.AnnounceSpell(def, fusion);

        if (fusion)
        {
            AudioSystem.Play(Sfx.Fusion, 0.85f, 0.82f + (def.Score * 0.005f));
        }
        else
        {
            float gain = def.Combo.Length <= 1 ? 0.42f : def.Combo.Length == 2 ? 0.55f : 0.7f;
            AudioSystem.Play(AudioSystem.ElementSfx(def.Element), gain, AudioSystem.ElementPitch(def.Element));
        }

        Vector2 aim = GameMath.FromAngle(p.AimAngle);
        Vector2 origin = p.Pos + aim * (p.Radius + 3f);
        string sheet = def.SheetKey;

        // Растровая анимация каста. Размер и точка зависят от вида заклинания,
        // чтобы вспышка совпадала с тем, что заклинание реально делает.
        switch (def.Kind)
        {
            case SpellKind.Nova:
                game.Effects.CastSpriteAt(p.Pos, sheet, def.Radius * 2.1f, 0.42f);
                break;
            case SpellKind.Cone:
                game.Effects.CastSpriteAt(p.Pos + aim * (def.Radius * 0.4f), sheet, def.Radius * 1.5f, 0.36f);
                break;
            case SpellKind.Field:
                game.Effects.CastSpriteAt(p.Pos + aim * 16f, sheet, def.Radius * 1.8f, 0.5f);
                break;
            case SpellKind.Beam:
                game.Effects.CastSpriteAt(p.Pos, sheet, def.BeamWidth * 5.5f, 0.34f);
                break;
            case SpellKind.Bolt:
                game.Effects.CastSpriteAt(origin, sheet, 46f, 0.3f);
                break;
            case SpellKind.Rain:
                // У дождя анимация идёт в точку падения, а не в игрока.
                game.Effects.CastSpriteAt(p.Pos, sheet, 64f, 0.32f);
                break;
        }


        switch (def.Kind)
        {
            case SpellKind.Bolt:
            {
                int count = def.Count + p.ExtraBolts;
                for (int i = 0; i < count; i++)
                {
                    float t = count == 1 ? 0f : i / (float)(count - 1) - 0.5f;
                    float a = p.AimAngle + t * def.Spread + game.Rng.Range(-0.04f, 0.04f);
                    Projectile pr = new()
                    {
                        Pos = origin + GameMath.FromAngle(a) * (i * 4f),
                        Vel = GameMath.FromAngle(a) * def.Speed,
                        Aim = GameMath.FromAngle(a),
                        Radius = def.Explode ? 5f : 3.5f,
                        Size = def.Explode ? 6f : 4f,
                        Damage = damage,
                        Color = def.Color,
                        Element = def.Element,
                        Pierce = def.Pierce,
                        Life = def.Life,
                        Owner = owner,
                        Explode = def.Explode,
                        ExplodeRadius = def.ExplodeRadius * (fusion ? 1.3f : 1f),
                        Slow = def.Slow,
                        Knockback = def.Knockback,
                        Stun = def.Stun,
                        Homing = def.Homing ? 3.2f : 0f,
                        Stretch = def.Speed > 220f,
                        Style = def.Element switch
                        {
                            Element.Earth => ProjectileStyle.Rock,
                            Element.Light => ProjectileStyle.Star,
                            Element.Dark => ProjectileStyle.Vortex,
                            Element.Water => ProjectileStyle.Shard,
                            Element.Wind => ProjectileStyle.Bolt,
                            _ => ProjectileStyle.Orb,
                        },
                        Wave = game.Rng.Range(0f, GameMath.Tau),
                    };
                    game.Projectiles.Add(pr);
                }
                game.Effects.Burst(origin, def.Color, 10 + def.Score * 2, 110f, 3f, 0.3f);
                break;
            }

            case SpellKind.Nova:
            {
                AreaDamage(game, p.Pos, def.Radius, damage, def.Color, def.Element, def.Knockback, def.Stun, def.Slow, def.Pull);
                game.Effects.Ring(p.Pos, def.Radius, def.Color, 0.42f, 3f, true);
                game.Effects.Ring(p.Pos, def.Radius * 0.65f, Color.White, 0.28f, 2f);
                game.Effects.Burst(p.Pos, def.Color, 40, def.Radius * 5.5f, 3f, 0.5f);
                if (def.Heal > 0f) p.Heal(def.Heal, true, game);
                break;
            }

            case SpellKind.Cone:
            {
                ConeDamage(game, p.Pos, aim, def.Radius, def.Angle, damage, def.Color, def.Element, def.Knockback, def.Stun, def.Slow);
                game.Effects.Burst(p.Pos + aim * (def.Radius * 0.45f), def.Color, 34, def.Radius * 4f, 3f, 0.45f, true, def.Angle, p.AimAngle - def.Angle * 0.5f);
                game.Effects.Ring(p.Pos + aim * (def.Radius * 0.4f), def.Radius * 0.6f, def.Color, 0.3f, 2f);
                if (def.Heal > 0f) p.Heal(def.Heal, true, game);
                break;
            }

            case SpellKind.Field:
            {
                MagicField f = game.Effects.Field(p.Pos + aim * 16f, def, def.Radius, def.Duration, damage, def.Pull, def.Slow, def.Stun, def.Knockback);
                f.Drift = aim * (fusion ? 60f : def.Kind == SpellKind.Field ? 46f : 0f);
                game.Effects.Ring(f.Pos, def.Radius, def.Color, 0.4f, 2f);
                game.Effects.Burst(f.Pos, def.Color, 26, 120f, 3f, 0.5f);
                break;
            }

            case SpellKind.Rain:
            {
                for (int i = 0; i < def.Count; i++)
                {
                    Vector2 target = p.Pos + game.Rng.InsideCircle(96f);
                    target.X = GameMath.Clamp(target.X, 20f, game.Level.PixelSize.X - 20f);
                    target.Y = GameMath.Clamp(target.Y, 20f, game.Level.PixelSize.Y - 20f);
                    if (game.Level.SolidAt(target))
                    {
                        target = p.Pos + game.Rng.InsideCircle(40f);
                        target.X = GameMath.Clamp(target.X, 20f, game.Level.PixelSize.X - 20f);
                        target.Y = GameMath.Clamp(target.Y, 20f, game.Level.PixelSize.Y - 20f);
                    }
                    game.Effects.Telegraph(target, def.Radius, def.Delay * (i + 1) + 0.18f, def.Color, damage, def.Knockback, def.Stun, sheet);
                }
                break;
            }

            case SpellKind.Beam:
            {
                float length = game.Level.RayLength(p.Pos, aim, def.BeamLength);
                BeamHit(game, p.Pos, aim, length, def.BeamWidth, damage, def);
                break;
            }
        }

        if (def.Kind is not SpellKind.Rain)
        {
            game.Effects.Burst(origin, def.Color, fusion ? 26 : 12, fusion ? 190f : 120f, 2.5f, 0.35f, true, 1.1f, p.AimAngle - 0.55f);
        }
    }

    public static void Hit(Game game, Enemy enemy, float amount, Vector2 from, float knockback, float stun, float slow, Element element)
    {
        Player p = game.Caster;
        float dmg = amount;
        if (game.Rng.Chance(p.CritChance)) dmg *= 2f;
        enemy.Hurt(game, dmg, from, element, knockback, stun, slow);
        if (p.Lifesteal > 0f) p.Heal(dmg * p.Lifesteal, false, game);
    }

    /// <summary>Нанести урон сопернику в дуэли (если он есть и жив).</summary>
    private static void HitFoe(Game game, Vector2 pos, float radius, float damage, Vector2 from, float knockback, Element element)
    {
        Player? foe = game.DuelFoeFor(game.Caster);
        if (foe is null || !foe.Alive) return;
        float d = Vector2.Distance(foe.Pos, pos);
        if (d > radius + foe.Radius) return;
        float falloff = GameMath.Clamp01(1.25f - d / (radius + foe.Radius));
        float dmg = damage * falloff;
        if (game.Rng.Chance(game.Caster.CritChance)) dmg *= 2f;
        foe.TakeDamage(game, dmg, from);
        game.Effects.Burst(foe.Pos, elementColor(element), 8, 110f, 2.5f, 0.3f);
    }

    internal static Color elementColor(Element element) => Elements.Color(element);

    /// <summary>Попадание по обоим: врагам (обычный режим) и сопернику (дуэль).</summary>
    public static void HitFoeOnly(Game game, Vector2 pos, float radius, float damage, Vector2 from, float knockback, Element element)
    {
        HitFoe(game, pos, radius, damage, from, knockback, element);
    }

    public static void AreaDamage(Game game, Vector2 center, float radius, float damage, Color color, Element element, float knockback, float stun, float slow, float pull = 0f)
    {
        for (int enemyIndex = 0; enemyIndex < game.Enemies.Count; enemyIndex++)
        {
            Enemy enemy = game.Enemies[enemyIndex];
            if (enemy.Dead || enemy.SpawnTimer > 0f) continue;
            float d = Vector2.Distance(enemy.Pos, center);
            float reach = radius + enemy.Radius;
            if (d > reach) continue;
            float falloff = GameMath.Clamp01(1.35f - d / reach);
            if (pull > 0f)
            {
                Vector2 dir = GameMath.Normalized(center - enemy.Pos);
                enemy.Vel += dir * pull * 0.35f;
            }
            Hit(game, enemy, damage * falloff, center, knockback * falloff, stun * falloff, slow, element);
            game.Effects.Burst(enemy.Pos, color, 6, 90f, 2.5f, 0.3f);
        }
        HitFoe(game, center, radius, damage, center, knockback, element);
    }

    public static void ConeDamage(Game game, Vector2 origin, Vector2 dir, float radius, float angle, float damage, Color color, Element element, float knockback, float stun, float slow)
    {
        for (int enemyIndex = 0; enemyIndex < game.Enemies.Count; enemyIndex++)
        {
            Enemy enemy = game.Enemies[enemyIndex];
            if (enemy.Dead || enemy.SpawnTimer > 0f) continue;
            Vector2 to = enemy.Pos - origin;
            float d = to.Length();
            if (d > radius + enemy.Radius) continue;
            float a = MathF.Abs(MathF.Atan2(MathF.Sin(GameMath.AngleOf(to) - GameMath.AngleOf(dir)), MathF.Cos(GameMath.AngleOf(to) - GameMath.AngleOf(dir))));
            if (a > angle * 0.5f) continue;
            float falloff = GameMath.Clamp01(1.3f - d / (radius + enemy.Radius));
            Hit(game, enemy, damage * falloff, origin, knockback * falloff, stun * falloff, slow, element);
            game.Effects.Burst(enemy.Pos, color, 5, 80f, 2.5f, 0.28f);
        }

        Player? foe = game.DuelFoeFor(game.Caster);
        if (foe is not null && foe.Alive)
        {
            Vector2 to = foe.Pos - origin;
            float d = to.Length();
            if (d <= radius + foe.Radius)
            {
                float a = MathF.Abs(MathF.Atan2(MathF.Sin(GameMath.AngleOf(to) - GameMath.AngleOf(dir)), MathF.Cos(GameMath.AngleOf(to) - GameMath.AngleOf(dir))));
                if (a <= angle * 0.5f)
                {
                    float falloff = GameMath.Clamp01(1.25f - d / (radius + foe.Radius));
                    HitFoe(game, origin, radius + d, damage, origin, knockback, element);
                }
            }
        }
    }

    public static void BeamHit(Game game, Vector2 origin, Vector2 dir, float length, float width, float damage, SpellDef def)
    {
        Vector2 side = new(-dir.Y, dir.X);
        for (int enemyIndex = 0; enemyIndex < game.Enemies.Count; enemyIndex++)
        {
            Enemy enemy = game.Enemies[enemyIndex];
            if (enemy.Dead || enemy.SpawnTimer > 0f) continue;
            Vector2 to = enemy.Pos - origin;
            float along = to.X * dir.X + to.Y * dir.Y;
            if (along < -enemy.Radius || along > length + enemy.Radius) continue;
            Vector2 closest = origin + dir * GameMath.Clamp(along, 0f, length);
            float d = Vector2.Distance(enemy.Pos, closest);
            if (d > width * 0.5f + enemy.Radius) continue;
            Hit(game, enemy, damage, closest, def.Knockback, def.Stun, def.Slow, def.Element);
        }

        Player? foe = game.DuelFoeFor(game.Caster);
        if (foe is not null && foe.Alive)
        {
            Vector2 to = foe.Pos - origin;
            float along = to.X * dir.X + to.Y * dir.Y;
            if (along >= -foe.Radius && along <= length + foe.Radius)
            {
                Vector2 closest = origin + dir * GameMath.Clamp(along, 0f, length);
                if (Vector2.Distance(foe.Pos, closest) <= width * 0.5f + foe.Radius)
                {
                    foe.TakeDamage(game, damage, origin);
                }
            }
        }

        Vector2 perpendicular = side * (width * 0.5f);
        Vector2 end = origin + dir * length;
        game.Effects.BeamFX(origin, end, perpendicular, def.Color, 0.28f);
        for (int i = 0; i < 26; i++)
        {
            float t = i / 25f;
            game.Effects.Burst(Vector2.Lerp(origin, end, t), def.Color, 3, 130f, 2.5f, 0.35f);
        }
    }

    public static void Explode(Game game, Projectile pr)
    {
        AreaDamage(game, pr.Pos, pr.ExplodeRadius, pr.Damage * 0.65f, pr.Color, pr.Element, pr.Knockback * 0.6f, pr.Stun * 0.5f, pr.Slow);
        game.Effects.Ring(pr.Pos, pr.ExplodeRadius, pr.Color, 0.34f, 2.5f, true);
        game.Effects.Burst(pr.Pos, pr.Color, 22, 150f, 3f, 0.45f);
        game.Effects.Smoke(pr.Pos, Palette.Fade(pr.Color, 0.4f), 8, 40f, 5f, 0.7f);
        game.Camera.Add(1.6f);
    }

    public static void ResolveTelegraph(Game game, Telegraph t)
    {
        if (t.Line)
        {
            Vector2 side = new Vector2(-t.Dir.Y, t.Dir.X) * (t.Width * 0.5f);
            Vector2 end = t.Pos + t.Dir * t.Length;
            Vector2 mid = t.Pos + t.Dir * (t.Length * 0.5f);
            for (int enemyIndex = 0; enemyIndex < game.Enemies.Count; enemyIndex++)
            {
                Enemy enemy = game.Enemies[enemyIndex];
                if (enemy.Dead || enemy.SpawnTimer > 0f) continue;
                if (DistToSegment(enemy.Pos, t.Pos, end) > t.Width * 0.5f + enemy.Radius) continue;
                Hit(game, enemy, t.Damage, mid, t.Knockback, t.Stun, 0f, Element.Fire);
            }

            // Раньше телеграф босса бил только его собственных минионов:
            // игрока в этом методе не было вообще, поэтому AoE был безвреден.
            Player p = game.Player;
            if (p.Alive && DistToSegment(p.Pos, t.Pos, end) <= t.Width * 0.5f + p.Radius)
            {
                p.TakeDamage(game, t.Damage, mid);
            }

            game.Effects.BeamFX(t.Pos, end, side, t.Color, 0.22f);
            game.Effects.Burst(end, t.Color, 20, 150f, 3f, 0.4f);
            game.Camera.Add(4f);
            return;
        }
        AreaDamage(game, t.Pos, t.Radius, t.Damage, t.Color, Element.Fire, t.Knockback, t.Stun, 0f);

        Player victim = game.Player;
        if (victim.Alive && Vector2.Distance(victim.Pos, t.Pos) <= t.Radius + victim.Radius)
        {
            victim.TakeDamage(game, t.Damage, t.Pos);
        }

        game.Effects.Ring(t.Pos, t.Radius, t.Color, 0.3f, 3f, true);
        game.Effects.Burst(t.Pos, t.Color, 20, 160f, 3f, 0.4f);

        // Анимацию удара дождя рисуем только для заклинаний игрока:
        // у телеграфов боссов Sheet пустой.
        if (!string.IsNullOrEmpty(t.Sheet))
        {
            game.Effects.CastSpriteAt(t.Pos, t.Sheet, t.Radius * 2.6f, 0.34f);
        }

        game.Camera.Add(2.6f);
    }

    public static void FieldTick(Game game, MagicField f)
    {
        if (f.Pull > 0f)
        {
            for (int enemyIndex = 0; enemyIndex < game.Enemies.Count; enemyIndex++)
            {
                Enemy enemy = game.Enemies[enemyIndex];
                if (enemy.Dead || enemy.SpawnTimer > 0f) continue;
                float d = Vector2.Distance(enemy.Pos, f.Pos);
                if (d > f.Radius) continue;
                Vector2 dir = GameMath.Normalized(f.Pos - enemy.Pos);
                enemy.Vel += dir * f.Pull * 0.3f;
            }
        }
        for (int enemyIndex = 0; enemyIndex < game.Enemies.Count; enemyIndex++)
        {
            Enemy enemy = game.Enemies[enemyIndex];
            if (enemy.Dead || enemy.SpawnTimer > 0f) continue;
            float d = Vector2.Distance(enemy.Pos, f.Pos);
            if (d > f.Radius + enemy.Radius * 0.5f) continue;
            Hit(game, enemy, f.Dps * 0.3f, f.Pos, f.Knockback * 0.3f, 0f, f.Slow, f.Element);
        }
        game.Effects.Burst(f.Pos + game.Rng.InsideCircle(f.Radius * 0.8f), f.Color, 3, 60f, 2.5f, 0.3f);
    }

    public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.LengthSquared();
        if (len2 < 0.0001f) return Vector2.Distance(p, a);
        float t = GameMath.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }

    public static void UpdateProjectile(Game game, Projectile pr, float dt)
    {
        pr.Age += dt;
        pr.Life -= dt;
        if (pr.Life <= 0f)
        {
            if (pr.Explode) Explode(game, pr);
            pr.Dead = true;
            return;
        }

        if (pr.Homing > 0f)
        {
            Enemy? best = null;
            float bestDist = 190f;
            for (int enemyIndex = 0; enemyIndex < game.Enemies.Count; enemyIndex++)
            {
                Enemy enemy = game.Enemies[enemyIndex];
                if (enemy.Dead || enemy.SpawnTimer > 0f || enemy.Intangible || pr.Hits.Contains(enemy.Id)) continue;
                float d = Vector2.Distance(enemy.Pos, pr.Pos);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = enemy;
                }
            }
            if (best is not null)
            {
                Vector2 want = GameMath.Normalized(best.Pos - pr.Pos) * pr.Speed;
                pr.Vel = Vector2.Lerp(pr.Vel, want, MathF.Min(1f, pr.Homing * dt));
            }
        }

        Vector2 next = game.Level.Move(pr.Pos, pr.Vel * dt, pr.Radius);
        if (MathF.Abs(next.X - pr.Pos.X) < 0.001f && MathF.Abs(next.Y - pr.Pos.Y) < 0.001f && pr.Speed > 1f)
        {
            if (pr.Explode) Explode(game, pr);
            else
            {
                game.Effects.Burst(pr.Pos, pr.Color, 5, 60f, 2f, 0.25f);
            }
            pr.Dead = true;
            return;
        }
        pr.Pos = next;

        pr.TrailTimer -= dt;
        if (pr.TrailTimer <= 0f)
        {
            pr.TrailTimer = 0.018f;
            game.Effects.AddParticle(new Particle
            {
                Pos = pr.Pos,
                Vel = -pr.Vel * 0.12f,
                Life = 0.26f,
                MaxLife = 0.26f,
                Size = pr.Size * 1.5f,
                Color = pr.Color,
                Additive = true,
                Drag = 3f,
            });
        }

        if (pr.Hostile)
        {
            Player p = game.Player;
            if (p.Alive && Vector2.Distance(pr.Pos, p.Pos) < pr.Radius + p.Radius)
            {
                p.TakeDamage(game, pr.Damage, pr.Pos);
                pr.Dead = true;
            }
            return;
        }

        if (pr.Owner >= 0)
        {
            Player? foe = game.DuelFoeForId((byte)pr.Owner);
            if (foe is not null && foe.Alive && Vector2.Distance(pr.Pos, foe.Pos) < pr.Radius + foe.Radius)
            {
                float dmg = pr.Damage;
                if (game.Rng.Chance(game.Caster.CritChance)) dmg *= 2f;
                foe.TakeDamage(game, dmg, pr.Pos);
                pr.Dead = true;
                if (pr.Explode) Explode(game, pr);
            }
            if (pr.Dead) return;
        }

        for (int enemyIndex = 0; enemyIndex < game.Enemies.Count; enemyIndex++)
        {
            Enemy enemy = game.Enemies[enemyIndex];
            if (enemy.Dead || enemy.SpawnTimer > 0f || enemy.Intangible) continue;
            if (pr.Hits.Contains(enemy.Id)) continue;
            if (Vector2.Distance(pr.Pos, enemy.Pos) > pr.Radius + enemy.Radius) continue;
            pr.Hits.Add(enemy.Id);
            Hit(game, enemy, pr.Damage, pr.Pos, pr.Knockback, pr.Stun, pr.Slow, pr.Element);
            game.Effects.Burst(pr.Pos, pr.Color, 7, 90f, 2.5f, 0.28f);
            game.Camera.Add(0.7f);
            if (pr.Explode)
            {
                Explode(game, pr);
                pr.Dead = true;
                return;
            }
            if (pr.Pierce-- <= 0)
            {
                pr.Dead = true;
                return;
            }
        }
    }
}
