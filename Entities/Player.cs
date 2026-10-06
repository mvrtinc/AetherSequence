using System.Drawing;
using System.Numerics;
using AetherSequence.Art;
using AetherSequence.Audio;
using AetherSequence.Combat;
using AetherSequence.Core;
using AetherSequence.Entities;

namespace AetherSequence.Entities;

internal sealed class Player
{
    public Vector2 Pos;
    public Vector2 Vel;
    public float Radius = 5f;

    public float Hp = 120f;
    public float MaxHp = 120f;
    public float Mana = 100f;
    public float MaxMana = 100f;
    public float ManaRegen = 16f;
    public float ManaRegenDelay = 0.25f;

    public float DamageMul = 1f;
    public float XpMul = 1f;
    public float CostMul = 1f;
    public float DamageTakenMul = 1f;
    public float CritChance = 0.05f;
    public float Lifesteal;
    public float FusionDamageMul = 1f;
    public float BurnAura;
    public float BurnAuraRadius = 34f;
    public float ManaOnKill = 1.2f;
    public float ManaOnDash = 3f;
    public int ExtraBolts;
    public float SpellCooldownMul = 1f;
    public float ResonanceWindow { get; set; } = 0.55f;

    public float Flow;
    public float DashCooldown = 0.52f;
    public float DashTimer;
    public float DashCooldownTimer;
    public float DashCost = 4f;
    public float StarvationCd;
    public bool Dashing;
    public Vector2 DashDir;

    public int Level = 1;
    public float Xp;
    public float XpToNext = 14f;

    public float Invuln;
    public float HitFlash;
    public float CastFlash;
    public float DeathTimer;
    public float AimAngle;
    public float MoveAngle;
    public bool Alive = true;

    /// <summary>Замедление от «проклятия» Гнилого Короля.</summary>
    public float SlowFactor = 1f;
    public float SlowTimer;

    public float AnimTime;
    public float WalkPhase;
    public float TrailTimer;
    public float ElementTintTimer;
    public Element LastElement;
    public Color ElementTint = Color.White;

    public readonly bool[] RuneUnlocked = { true, true, true, false, true, false };

    public readonly SpellBuffer Buffer = new();

    public int SelectedRune;

    public float FireHoldTime;

    public float AutoCastTimer;

    public float FireHoldThreshold = 0.25f;

    public SpellDef? LastCast { get; private set; }

    public bool LastCastWasFusion { get; private set; }

    public Element SelectedElement => Runes.Order[SelectedRune];

    public void CycleRune(int direction)
    {
        for (int i = 0; i < 6; i++)
        {
            SelectedRune = (SelectedRune + direction + 6) % 6;
            if (RuneUnlocked[SelectedRune]) return;
        }
    }

    public void SelectRune(int slot)
    {
        if (slot >= 0 && slot < 6 && RuneUnlocked[slot]) SelectedRune = slot;
    }

    public void RememberCast(SpellDef def, bool fusion)
    {
        LastCast = def;
        LastCastWasFusion = fusion;
    }

    public void RepeatLast(Game game)
    {
        if (LastCast is null) return;
        CombatUtil.Cast(game, this, LastCast, false, repeated: true);
    }

    public int UnlockedCount
    {
        get
        {
            int n = 0;
            foreach (bool b in RuneUnlocked)
            {
                if (b) n++;
            }
            return n;
        }
    }

    public float DamageMultiplier => DamageMul * (1f + Flow * 0.4f) * (1f + (Level - 1) * 0.05f);

    public float XpToNextLevel => 14f + Level * 10f + Level * Level * 0.8f;

    public void Teleport(Vector2 spawn)
    {
        Pos = spawn;
        Vel = Vector2.Zero;
        Dashing = false;
        DashCooldownTimer = 0f;
        Invuln = MathF.Max(Invuln, 0.6f);
        Buffer.Clear();
    }

    public void Update(Game game, float dt)
    {
        Update(game, game.Input, dt);
    }

    /// <summary>Обновление от сетевого ввода соперника (используется хостом).</summary>
    public void UpdateRemote(Game game, AetherSequence.Net.RemoteInputState state, float dt)
    {
        AimFromRemote(state);
        Update(game, new AetherSequence.Net.RemoteInputView(state), dt);
    }

    private void AimFromRemote(AetherSequence.Net.RemoteInputState state)
    {
        if (!state.Any) return;
        Vector2 delta = new Vector2(state.MouseX, state.MouseY) + _aimCamera - Pos;
        if (delta.LengthSquared() > 4f) AimAngle = GameMath.AngleOf(delta);
    }

    private Vector2 _aimCamera;

    /// <summary>Позиция камеры, от которой отсчитывается прицел сетевого игрока.</summary>
    public Vector2 AimCamera
    {
        get => _aimCamera;
        set => _aimCamera = value;
    }

    public void Update(Game game, IPlayerInput input, float dt)
    {
        AnimTime += dt;
        Invuln = MathF.Max(0f, Invuln - dt);
        HitFlash = MathF.Max(0f, HitFlash - dt * 5f);
        CastFlash = MathF.Max(0f, CastFlash - dt * 4f);
        ElementTintTimer = MathF.Max(0f, ElementTintTimer - dt);
        DashCooldownTimer = MathF.Max(0f, DashCooldownTimer - dt);
        StarvationCd = MathF.Max(0f, StarvationCd - dt);

        Vector2 move = input.Move;
        if (move.LengthSquared() > 0.01f)
        {
            MoveAngle = GameMath.AngleOf(move);
            WalkPhase += dt * 11f;
        }
        else
        {
            WalkPhase = GameMath.Approach(WalkPhase, 0f, dt * 8f);
        }

        if (Dashing)
        {
            DashTimer -= dt;
            Vel = DashDir * 330f;
            TrailTimer -= dt;
            if (TrailTimer <= 0f)
            {
                TrailTimer = 0.025f;
                game.Effects.AddParticle(new Particle
                {
                    Pos = Pos,
                    Vel = -DashDir * 20f,
                    Life = 0.28f,
                    MaxLife = 0.28f,
                    Size = 4f,
                    Color = GameMath.Mix(Elements.Color(LastElement), Color.White, 0.3f),
                    Additive = true,
                    Drag = 1f,
                });
            }
            if (DashTimer <= 0f) Dashing = false;
        }
        else
        {
            Vel += move * 1500f * dt;
            float drag = move.LengthSquared() > 0.01f ? 9f : 13f;
            Vel *= MathF.Max(0f, 1f - drag * dt);
            if (Vel.Length() > 132f) Vel = GameMath.ClampLength(Vel, 132f);
        }

        if (input.Pressed(InputAction.Dash))
        {
            TryDash(game, move);
        }

        Pos = game.Level.Move(Pos, Vel * dt * SlowFactor, Radius);

        // «Проклятие» Короля истлевает: пока таймер жив, движение замедлено.
        if (SlowTimer > 0f)
        {
            SlowTimer -= dt;
            if (SlowTimer <= 0f) SlowFactor = 1f;
        }

        ManaRegenDelay = MathF.Max(0f, ManaRegenDelay - dt);
        if (ManaRegenDelay <= 0f && Mana < MaxMana)
        {
            Mana = MathF.Min(MaxMana, Mana + ManaRegen * dt);
        }

        Flow = MathF.Max(0f, Flow - dt * 0.3f);

        for (int slot = 0; slot < 6; slot++)
        {
            InputAction action = slot switch
            {
                0 => InputAction.Cast1,
                1 => InputAction.Cast2,
                2 => InputAction.Cast3,
                3 => InputAction.Cast4,
                4 => InputAction.Cast5,
                _ => InputAction.Cast6,
            };
            if (input.Pressed(action) && RuneUnlocked[slot])
            {
                SelectRune(slot);
                PushRune(game, Runes.Order[slot], false);
            }
        }

        bool fireDown = game.Settings.MouseAutoFire && input.Down(InputAction.Fire);
        FireHoldTime = fireDown ? input.HoldTime(InputAction.Fire) : 0f;
        if (input.Pressed(InputAction.Fire))
        {
            AutoCastTimer = game.Settings.AutoCastInterval;
            PushRune(game, SelectedElement, false);
        }
        else if (fireDown)
        {
            AutoCastTimer -= dt;
            if (AutoCastTimer <= 0f && Buffer.CommitLeft <= 0f)
            {
                AutoCastTimer = game.Settings.AutoCastInterval;
                PushRune(game, SelectedElement, FireHoldTime >= FireHoldThreshold);
            }
        }
        else
        {
            AutoCastTimer = 0f;
        }

        if (game.Settings.QuickCastEnabled && input.Pressed(InputAction.QuickCast))
        {
            RepeatLast(game);
        }

        if (input.Pressed(InputAction.ClearCombo))
        {
            if (Buffer.Flush(out SpellDef? flushed, out bool flushFusion))
            {
                CombatUtil.Cast(game, this, flushed!, flushFusion);
            }
        }

        Buffer.Window = ResonanceWindow;
        Buffer.CommitMax = game.Settings.ComboWindow;
        if (Buffer.Update(dt, out SpellDef? spell, out bool fusion))
        {
            CombatUtil.Cast(game, this, spell!, fusion);
        }

        if (BurnAura > 0f)
        {
            game.AuraTick += dt;
            if (game.AuraTick >= 0.4f)
            {
                game.AuraTick = 0f;
                for (int i = 0; i < game.Enemies.Count; i++)
                {
                    Enemy e = game.Enemies[i];
                    if (e.Dead) continue;
                    if (Vector2.Distance(e.Pos, Pos) <= BurnAuraRadius)
                    {
                        e.Hurt(game, BurnAura, Pos, Element.Fire, 0f, 0f, 0f);
                    }
                }
            }
        }
    }

    private void PushRune(Game game, Element element, bool flushFirst)
    {
        LastElement = element;
        ElementTint = Elements.Color(element);
        ElementTintTimer = 0.45f;
        if (flushFirst && Buffer.RuneCount > 0)
        {
            Buffer.Clear();
        }
        if (Buffer.Push(element, out SpellDef? spell, out bool fusion))
        {
            CombatUtil.Cast(game, this, spell!, fusion);
        }
    }

    private void TryDash(Game game, Vector2 move)
    {
        if (Dashing || DashCooldownTimer > 0f) return;
        Vector2 dir = move.LengthSquared() > 0.01f ? move : GameMath.FromAngle(AimAngle);
        Dashing = true;
        DashDir = dir;
        DashTimer = 0.18f;
        DashCooldownTimer = DashCooldown * SpellCooldownMul;
        Mana = MathF.Max(0f, Mana - DashCost);
        Invuln = MathF.Max(Invuln, 0.26f);
        game.Camera.Add(1.4f);
        AudioSystem.Play(Sfx.Dash, 0.45f, 0.9f + game.Rng.NextFloat() * 0.25f);
        game.Effects.Burst(Pos - dir * 4f, GameMath.Mix(Elements.Color(LastElement), Color.White, 0.4f), 10, 90f, 2.5f, 0.3f);
        if (ManaOnDash > 0f) Mana = MathF.Min(MaxMana, Mana + ManaOnDash);
    }

    public bool CanPay(int cost) => Mana >= cost * CostMul;

    public void Pay(int cost)
    {
        float c = cost * CostMul;
        if (Mana >= c)
        {
            Mana -= c;
            ManaRegenDelay = 0.25f;
            return;
        }

        float missing = c - Mana;
        Mana = 0f;
        if (StarvationCd <= 0f)
        {
            StarvationCd = 0.5f;
            TakeDamage(null, missing * 0.35f, Pos, true);
        }
    }

    public void TakeDamage(Game? game, float amount, Vector2 from, bool selfInflicted = false)
    {
        if (!Alive) return;
        if (!selfInflicted && (Invuln > 0f || Dashing)) return;
        float dmg = amount * DamageTakenMul;
        Hp -= dmg;
        HitFlash = 1f;
        game?.BreakCharge();
        if (game is not null)
     {
            game.Camera.Add(selfInflicted ? 1.2f : 3.4f);
            if (!selfInflicted) AudioSystem.Play(Sfx.PlayerHurt, 0.6f, 1f - Hp / MaxHp * 0.25f);
            game.Flash(Palette.Danger, selfInflicted ? 0.2f : 0.35f);
            game.Effects.Label(Pos + new Vector2(0f, -10f), $"-{MathF.Round(dmg)}", Palette.Health);
            game.Effects.Burst(Pos, Palette.Health, 8, 70f, 2f, 0.35f);
        }
        if (!selfInflicted)
        {
            Invuln = 0.85f;
            Vector2 push = GameMath.Normalized(Pos - from);
            Vel += push * 130f;
        }
        if (Hp <= 0f)
        {
            Hp = 0f;
            Alive = false;
            DeathTimer = 0f;
        }
    }

    public void Heal(float amount, bool showText = true, Game? game = null)
    {
        if (Hp <= 0f) return;
        float before = Hp;
        Hp = MathF.Min(MaxHp, Hp + amount);
        if (showText && game is not null && Hp > before)
        {
            game.Effects.Label(Pos + new Vector2(0f, -14f), $"+{MathF.Round(Hp - before)}", GameMath.Rgb(120, 240, 150));
        }
    }

    public void AddMana(float amount, bool showText = false, Game? game = null)
    {
        float before = Mana;
        Mana = MathF.Min(MaxMana, Mana + amount);
        if (showText && game is not null && Mana > before)
        {
            game.Effects.Label(Pos + new Vector2(8f, -6f), $"+{MathF.Round(Mana - before)} маны", Palette.Mana);
        }
    }

public void Draw(Graphics g, float time)
       {
           float bob = MathF.Sin(AnimTime * 3.4f) * 0.8f;
           float w = Sprites.Mage.Width;
           float h = Sprites.Mage.Height;
           float drawX = Pos.X - w * 0.5f;
           float drawY = Pos.Y - h + 5f + bob;
           bool flip = AimAngle > MathF.PI * 0.5f || AimAngle < -MathF.PI * 0.5f;

           // Тень под ногами: персонаж стоит на поверхности, а не висит в кадре.
    Palette.Disc(g, Palette.Fade(Color.Black, 0.42f), Pos.X, Pos.Y + 6f, 5.5f);

      bool flash = HitFlash > 0.05f;

           // След при быстром движении: несколько ослабленных силуэтов
           // позади. Пара пикселей остаточного следа даёт ощущение скорости.
        float speed = Vel.Length();
   if (speed > 90f)
   {
  float trail = GameMath.Clamp01((speed - 90f) / 260f) * 0.3f;
 Color tint = GameMath.Mix(Elements.Color(LastElement), Color.White, 0.35f);
        Vector2 back = GameMath.Normalized(Vel) * 3.4f;
        Sprites.Mage.DrawSilhouette(g, drawX - back.X, drawY - back.Y, tint, trail, flip, 1f);
      Sprites.Mage.DrawSilhouette(g, drawX - back.X * 2f, drawY - back.Y * 2f, tint, trail * 0.5f, flip, 1f);
     }

    if (Dashing)
    {
        Sprites.Mage.DrawSilhouette(g, drawX - DashDir.X * 3f, drawY, GameMath.Mix(Elements.Color(LastElement), Color.White, 0.4f), 0.28f, flip, 1f);
      Sprites.Mage.DrawSilhouette(g, drawX - DashDir.X * 6f, drawY, GameMath.Mix(Elements.Color(LastElement), Color.White, 0.4f), 0.16f, flip, 1f);
   }

  if (flash)
  {
     Sprites.Mage.DrawSilhouette(g, drawX, drawY, Color.White, HitFlash, flip, 1f);
   }
   else
  {
       Sprites.Mage.Draw(g, drawX, drawY, flip, 1f);
       }

    // Контур: спрайт рисуется чуть крупнее тёмным силуэтом под собой.
    // Один пиксель контура отделяет персонажа от любого фона.
    Sprites.Mage.DrawSilhouette(g, drawX, drawY, GameMath.Rgb(6, 5, 12), 0.85f, flip, 1f);
  Sprites.Mage.Draw(g, drawX, drawY, flip, 1f);

       if (CastFlash > 0.02f)
 {
     Palette.AddGlow(g, Pos + GameMath.FromAngle(AimAngle) * 6f, 9f * CastFlash, ElementTint, CastFlash * 0.8f);
        }

     if (Invuln > 0f && !Dashing && (int)(time * 22f) % 2 == 0)
    {
            Sprites.Mage.DrawSilhouette(g, drawX, drawY, Palette.Health, 0.22f, flip, 1f);
   }

      // Rim light снизу-сзади: ловит свет от пола и подсвечивает фигуру.
   DrawRimLight(g, drawX, drawY, w, h, flip);

    Palette.AddGlow(g, Pos, 16f, GameMath.Mix(ElementTint, Elements.Color(LastElement), 0.4f), 0.28f + Flow * 0.25f);
       }

    /// <summary>
    /// Контровой свет по нижней кромке силуэта. Персонаж стоит в освещённом
    /// пятне, и такая подсветка связывает его с этим светом.
    /// </summary>
    private void DrawRimLight(Graphics g, float drawX, float drawY, float w, float h, bool flip)
    {
   float pulse = 0.5f + 0.5f * MathF.Sin(AnimTime * 3.4f);
   Color rim = GameMath.Mix(Elements.Color(LastElement), Color.White, 0.45f);
   float alpha = 0.20f + 0.12f * pulse + Flow * 0.15f;

        // Нижняя кромка плаща.
        Sprites.MageCape.DrawSilhouette(g, drawX + 1f, drawY + h - 8f, rim, alpha, flip, 1f);
    }
}
