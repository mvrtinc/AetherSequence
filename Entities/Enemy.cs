using System.Drawing;
using System.Numerics;
using AetherSequence.Art;
using AetherSequence.Audio;
using AetherSequence.Combat;
using AetherSequence.Core;

namespace AetherSequence.Entities;

internal enum EnemyKind
{
    Slime,
    MiniSlime,
    Knight,
    Shade,
    Sentinel,
    Boss,
}

/// <summary>Какой из трёх боссов стоит на глубине 5 / 10 / 15.</summary>
internal enum BossKind
{
    Warden,      // Страж Эфира - глубина 5
    RotKing,     // Гнилой Король - глубина 10
    AshGolem,    // Пепельный Голем - глубина 15
}

internal static class BossDef
{
    public sealed record Data(
        string Name,
        BossKind Kind,
        float HpPerDepth,
        float Radius,
        float Speed,
        float ContactDamage,
        int XpValue,
        Color Tint,
        Color Crystal,
        Color Body,
        string Taunt);

    public static readonly Data[] All =
    {
        new("СТРАЖ ЭФИРА", BossKind.Warden, 100f, 13f, 34f, 18f, 120,
            GameMath.Rgb(255, 190, 120), GameMath.Rgb(150, 110, 255), GameMath.Rgb(64, 70, 96),
            "не дай Стражу дотронуться до тебя"),

        new("ГНИЛОЙ КОРОЛЬ", BossKind.RotKing, 148f, 14f, 30f, 20f, 160,
            GameMath.Rgb(206, 232, 168), GameMath.Rgb(150, 255, 190), GameMath.Rgb(86, 92, 76),
            "король зовёт костяную стражу - не дай ему собрать её"),

        new("ПЕПЕЛЬНЫЙ ГОЛЕМ", BossKind.AshGolem, 205f, 17f, 24f, 26f, 220,
            GameMath.Rgb(255, 148, 78), GameMath.Rgb(255, 208, 120), GameMath.Rgb(92, 56, 46),
            "скала проснулась: пока горит - она идёт"),
    };

    /// <summary>Босс для глубины. Глубины 5, 10 и 15 дают трёх разных.</summary>
    public static Data ForDepth(int depth) => All[(Math.Max(1, depth) / 5 - 1) % All.Length];

    public static BossKind KindForDepth(int depth) => ForDepth(depth).Kind;
}

internal sealed class Enemy
{
    public EnemyKind Kind;
    public Vector2 Pos;
    public Vector2 Vel;
    public float Radius = 6f;
    public float Hp = 20f;
    public float MaxHp = 20f;
    public float Speed = 40f;
    public float ContactDamage = 8f;

    /// <summary>Освещённость врага в этом кадре: 0 - тьма, 1 - полный свет.</summary>
    public float LightLevel;

    /// <summary>
    /// Обновляет LightLevel из маски света. В дуэли и на аренах темноты нет,
    /// поэтому там всегда 1 и поведение остаётся прежним.
    /// </summary>
    private void LightAt(Game game) => LightLevel = game.Renderer?.LightAt(Pos) ?? 1f;

    /// <summary>
    /// Пробуждение. Разбужденный враг держится активным, пока не истечёт
    /// таймер, даже если свет погас: увидев игрока, он его не забудет.
    /// </summary>
    private void UpdateAwaken(float dt)
    {
        if (AwakenTimer > 0f) AwakenTimer = MathF.Max(0f, AwakenTimer - dt);

        // Свет сам по себе будит: если посох направлен прямо на врага.
        if (!Awakened && LightLevel > 0.45f)
        {
            Awakened = true;
            AwakenTimer = 10f;
        }
    }
    public float XpValue = 6f;
    public Color Tint = Color.White;
    public float Flash;
    public float Stun;
    public float SlowFactor = 1f;
    public float SlowTimer;
    public float Angle;
    public int State;
    public float StateTimer;
    public float AttackCd;
    public bool Dead;
    public float DeathTimer;
    public float AnimTime;
    public float Wave;
    public int Id;
    public float ContactCd;
    public float Phase;
    public bool Intangible;
    public float Scale = 1f;
    public float SpawnTimer = 0.35f;
    public float TelegraphAngle;
    public float TelegraphLength = 90f;
    public int PatternIndex;
    public float ChargeDir;
    public float Age;

    /// <summary>
    /// Разбужен ли враг. В темноте враги спят, пока игрок не зажжёт кристалл:
 /// свет в комнате будит тех, кто в ней есть, и притягивает тех, кто рядом.
 /// </summary>
    public bool Awakened;

    /// <summary>Сколько секунд враг остаётся разбуждённым.</summary>
    public float AwakenTimer;

    /// <summary>Боится света: в освещённом месте не бьёт и отступает.</summary>
    public bool FearsLight;

    /// <summary>Тянется к свету: сам идёт в освещённые комнаты.</summary>
    public bool DrawnToLight;

    private static int _nextId;

    public bool IsBoss => Kind == EnemyKind.Boss;

    /// <summary>Какой из трёх боссов (у обычных врагов - Warden по умолчанию).</summary>
    public BossKind Boss;

    /// <summary>Индекс босса в BossDef.All - для выбора набора частей спрайта.</summary>
    public int BossVariant;

    /// <summary>Имя босса для HUD.</summary>
    public string BossName = BossDef.All[0].Name;

    /// <summary>Кристалл в теле босса - задаётся палитрой темы.</summary>
    public Color BossCrystal = BossDef.All[0].Crystal;

    /// <summary>Основной цвет корпуса босса.</summary>
    public Color BossBody = BossDef.All[0].Body;

    /// <summary>Открытая враждебная зона: босс не стойкий, а телеграфит удар.</summary>
    public float ShockCooldown;

    public static Enemy Create(EnemyKind kind, Vector2 pos, int depth, Rng rng)
    {
        Enemy e = new() { Kind = kind, Pos = pos, Wave = rng.Range(0f, GameMath.Tau), Id = ++_nextId };
        float d = MathF.Max(0, depth - 1);
        switch (kind)
        {
            case EnemyKind.Slime:
                e.Radius = 7f;
                e.MaxHp = e.Hp = 22f + d * 3.2f;
                e.Speed = 46f;
                e.ContactDamage = 7f + d * 0.5f;
e.XpValue = 6f + d * 0.4f;
        e.Tint = GameMath.Rgb(120, 230, 170);
     e.AttackCd = rng.Range(0.6f, 1.6f);
              // Слизни лезут к свету: как только кристалл зажжён, они идут туда.
              e.DrawnToLight = true;
      break;
            case EnemyKind.MiniSlime:
                e.Radius = 4f;
                e.MaxHp = e.Hp = 9f + d * 1.1f;
                e.Speed = 62f;
                e.ContactDamage = 5f;
                e.XpValue = 3f;
                e.Tint = GameMath.Rgb(150, 240, 190);
                e.Scale = 0.75f;
                e.StateTimer = rng.Range(0.2f, 0.6f);
                e.SpawnTimer = 0.15f;
                break;
            case EnemyKind.Knight:
                e.Radius = 6.5f;
                e.MaxHp = e.Hp = 56f + d * 7f;
                e.Speed = 52f;
                e.ContactDamage = 14f + d * 0.8f;
                e.XpValue = 14f + d * 0.9f;
                e.Tint = GameMath.Rgb(120, 255, 220);
                e.AttackCd = rng.Range(0.4f, 1.4f);
                break;
            case EnemyKind.Sentinel:
                e.Radius = 7f;
                e.MaxHp = e.Hp = 66f + d * 8f;
                e.Speed = 0f;
                e.ContactDamage = 8f + d * 0.4f;
                e.XpValue = 16f + d * 1.1f;
                e.Tint = GameMath.Rgb(102, 255, 224);
                e.AttackCd = rng.Range(0.6f, 1.6f);
                break;
            case EnemyKind.Shade:
                e.Radius = 5.5f;
                e.MaxHp = e.Hp = 32f + d * 4.4f;
                e.Speed = 74f;
                e.ContactDamage = 15f + d * 0.9f;
e.XpValue = 11f + d * 0.7f;
        e.Tint = GameMath.Rgb(200, 170, 255);
        e.AttackCd = rng.Range(1.2f, 2.6f);
        // Тени боятся света: в освещённом не бьют и отходят в темноту.
  // Отсюда же они срывают зарядку - подойти вплотную в темноте проще.
     e.FearsLight = true;
        break;
            case EnemyKind.Boss:
            {
                BossDef.Data def = BossDef.ForDepth(depth);
                e.Boss = def.Kind;
                e.BossVariant = Math.Clamp(depth / 5 - 1, 0, BossDef.All.Length - 1);
                e.BossName = def.Name;
                e.BossCrystal = def.Crystal;
                e.BossBody = def.Body;
                e.Radius = def.Radius;
                e.MaxHp = e.Hp = 420f + d * def.HpPerDepth;
                e.Speed = def.Speed;
                e.ContactDamage = def.ContactDamage + d * 1.2f;
                e.XpValue = def.XpValue;
                e.Tint = def.Tint;
                e.AttackCd = 1.2f;
                e.ShockCooldown = 3f;
                break;
            }
        }
        return e;
    }

    public void Update(Game game, float dt)
    {
        Age += dt;
        AnimTime += dt;
        Flash = MathF.Max(0f, Flash - dt * 5f);
        SlowTimer -= dt;
        if (SlowTimer <= 0f) SlowFactor = 1f;
        ContactCd = MathF.Max(0f, ContactCd - dt);
        SpawnTimer = MathF.Max(0f, SpawnTimer - dt);
        AttackCd -= dt;

        if (Dead)
        {
            DeathTimer -= dt;
            return;
        }

        if (Stun > 0f)
        {
            Stun -= dt;
            Vel *= MathF.Max(0f, 1f - 6f * dt);
            Integrate(game, dt);
            return;
        }

Vector2 toPlayer = game.Player.Pos - Pos;
        float dist = toPlayer.Length();

        // Насколько вр��га сейчас освещён. Раньше такой проверки не было
     // вовсе, поэтому все враги вели себя одинаково в свете и в темноте.
        LightAt(game);
        UpdateAwaken(dt);

        // В темноте и не разбуженный враг не двигается - он спит, пока
        // свет не дойдёт до него. Это и есть базовое правило новой механики.
        bool dormant = !Awakened && LightLevel < 0.3f;
        if (dormant && !IsBoss)
        {
      Vel *= MathF.Max(0f, 1f - 6f * dt);
            Integrate(game, dt);
            return;
        }

        // Страх света: в освещённом месте враг не идёт на игрока, а
// отступает в темноту. Урон он при этом всё ещё может нанести вплотную.
        if (FearsLight && !IsBoss)
   {
   if (LightLevel > 0.5f && dist < 70f)
      {
        Vector2 away = GameMath.Normalized(Pos - game.Player.Pos);
        Vel += away * 190f * dt;
    Vel = GameMath.ClampLength(Vel, Speed * 0.9f);
                State = 0;
                StateTimer = 0.2f;
        AttackCd = MathF.Max(AttackCd, 0.4f);
    Integrate(game, dt);
                return;
      }
        }

  switch (Kind)
        {
            case EnemyKind.Slime:
 case EnemyKind.MiniSlime:
     UpdateSlime(game, dt, dist);
         break;
    case EnemyKind.Knight:
                UpdateKnight(game, dt, dist, toPlayer);
         break;
            case EnemyKind.Shade:
          UpdateShade(game, dt, dist, toPlayer);
             break;
            case EnemyKind.Sentinel:
UpdateSentinel(game, dt, dist, toPlayer);
         break;
            case EnemyKind.Boss:
      UpdateBoss(game, dt, dist, toPlayer);
          break;
        }

        Integrate(game, dt);
        TouchPlayer(game);
    }

    private void Integrate(Game game, float dt)
    {
        float factor = SlowFactor;
        Vector2 delta = Vel * (factor * dt);
        Vector2 next = game.Level.Move(Pos, delta, Radius);
        if (MathF.Abs(next.X - Pos.X) < 0.01f && Vel.X != 0f && Kind == EnemyKind.Knight && State == 2)
        {
            Stun = 1.5f;
            Vel = Vector2.Zero;
            game.Effects.Burst(Pos, Palette.WallEdge, 10, 80f, 2f, 0.35f);
            game.Camera.Add(2.5f);
        }
        Pos = next;
    }

    private void TouchPlayer(Game game)
    {
        if (Intangible || ContactCd > 0f || game.State != GameState.Playing) return;
        Player p = game.Player;
        float reach = Radius + p.Radius;
        if (Vector2.Distance(Pos, p.Pos) > reach) return;
        p.TakeDamage(game, ContactDamage, Pos);
        ContactCd = 0.6f;
        Vector2 push = GameMath.Normalized(p.Pos - Pos);
        Vel = push * (Kind == EnemyKind.Boss ? 90f : 150f);
    }

    private void UpdateSlime(Game game, float dt, float dist)
    {
        StateTimer -= dt;
        if (StateTimer <= 0f)
        {
            State = State == 0 ? 1 : 0;
            StateTimer = State == 1 ? 0.45f : 0.5f;
            if (State == 1)
            {
                Vector2 dir = GameMath.Normalized(game.Player.Pos - Pos);
                Vel = dir * (Speed * 2.4f);
            }
        }
        if (State == 0)
        {
            Vel *= MathF.Max(0f, 1f - 5f * dt);
        }
        Angle = MathF.Atan2(game.Player.Pos.Y - Pos.Y, game.Player.Pos.X - Pos.X);
        Scale = 1f + (State == 1 ? 0.12f : -0.08f) * GameMath.Clamp(1f - StateTimer * 2f, 0f, 1f);
    }

    private void UpdateKnight(Game game, float dt, float dist, Vector2 toPlayer)
    {
        Angle = GameMath.ApproachAngle(Angle, GameMath.AngleOf(toPlayer), dt * 6f);
        switch (State)
        {
            case 1:
                StateTimer -= dt;
                TelegraphAngle = MathF.Atan2(toPlayer.Y, toPlayer.X);
                Vel *= MathF.Max(0f, 1f - 8f * dt);
                if (StateTimer <= 0f)
                {
                    State = 2;
                    StateTimer = 0.5f;
                    ChargeDir = TelegraphAngle;
                    Vel = GameMath.FromAngle(ChargeDir) * 340f;
                    game.Effects.Burst(Pos, Palette.Rgb(120, 255, 220), 8, 100f, 2f, 0.3f);
                    AudioSystem.PlayAt(Sfx.Telegraph, Pos, game.Player.Pos, 0.45f);
                }
                break;
            case 2:
                StateTimer -= dt;
                if (StateTimer <= 0f)
                {
                    State = 3;
                    StateTimer = 0.45f;
                    Vel *= 0.3f;
                }
                break;
            case 3:
                StateTimer -= dt;
                Vel *= MathF.Max(0f, 1f - 5f * dt);
                if (StateTimer <= 0f) State = 0;
                break;
            default:
                if (dist > 150f)
                {
                    Vel += GameMath.Normalized(toPlayer) * 240f * dt;
                    Vel = GameMath.ClampLength(Vel, Speed);
                }
                else
                {
                    Vel *= MathF.Max(0f, 1f - 4f * dt);
                    if (dist < 90f)
                    {
                        Vel += GameMath.Normalized(-toPlayer) * 120f * dt;
                    }
                }
                if (AttackCd <= 0f && dist < 190f)
                {
                    if (dist < 165f && game.Level.LineClear(Pos, game.Player.Pos))
                    {
                        State = 1;
                        StateTimer = 0.62f;
                        AttackCd = 2.6f;
                    }
                    else if (dist > 70f)
                    {
                        Shoot(game, 1, 0f, 118f, 9f, Palette.Rgb(120, 255, 220));
                        AttackCd = 2.1f;
                    }
                }
                break;
        }
    }

    private void UpdateShade(Game game, float dt, float dist, Vector2 toPlayer)
    {
        float weave = GameMath.AngleOf(toPlayer) + MathF.Sin(AnimTime * 3.4f) * 0.6f;
        switch (State)
        {
            case 1:
                StateTimer -= dt;
                Intangible = StateTimer > 0f;
                Vel *= MathF.Max(0f, 1f - 6f * dt);
                if (StateTimer <= 0f)
                {
                    State = 2;
                    StateTimer = 0.3f;
                    Intangible = false;
                    Angle = MathF.Atan2(toPlayer.Y, toPlayer.X);
                    Vel = GameMath.FromAngle(Angle) * 430f;
                    game.Effects.Burst(Pos, Palette.Rgb(200, 170, 255), 10, 90f, 2.5f, 0.3f);
                }
                break;
            case 2:
                StateTimer -= dt;
                if (StateTimer <= 0f)
                {
                    State = 0;
                    Vel *= 0.2f;
                    AttackCd = 2.4f;
                }
                break;
            default:
                if (AttackCd <= 0f)
                {
                    State = 1;
                    StateTimer = 0.65f;
                    break;
                }
                Vel += GameMath.FromAngle(weave) * 320f * dt;
                Vel = GameMath.ClampLength(Vel, Speed);
                break;
        }
        if (State == 0) Angle = GameMath.AngleOf(Vel);
    }

    private void UpdateSentinel(Game game, float dt, float dist, Vector2 toPlayer)
    {
        Angle = GameMath.ApproachAngle(Angle, GameMath.AngleOf(toPlayer), dt * 1.6f);
        Vel *= MathF.Max(0f, 1f - 6f * dt);
        if (AttackCd <= 0f && dist < 230f)
        {
            Shoot(game, 3, 0.36f, 132f, 10f, Palette.Rgb(102, 255, 224));
            AttackCd = 1.9f;
            game.Effects.Burst(Pos + GameMath.FromAngle(Angle) * 7f, Palette.Rgb(102, 255, 224), 6, 70f, 2f, 0.25f);
        }
    }

    private void UpdateBoss(Game game, float dt, float dist, Vector2 toPlayer)
    {
        float hp01 = GameMath.Clamp01(Hp / MaxHp);
        float newPhase = hp01 > 0.66f ? 0f : hp01 > 0.33f ? 1f : 2f;
        if (newPhase > Phase)
        {
            Phase = newPhase;
            AudioSystem.PlayAt(Sfx.BossPhase, Pos, game.Player.Pos, 0.75f);
            game.Effects.Ring(Pos, 60f, Palette.Danger, 0.6f, 3f);
        }
        Angle = GameMath.ApproachAngle(Angle, MathF.Atan2(toPlayer.Y, toPlayer.X), dt * 1.8f);

        switch (State)
        {
            case 1:
                StateTimer -= dt;
                Vel *= MathF.Max(0f, 1f - 6f * dt);
                if (StateTimer <= 0f)
                {
                    State = 2;
                    StateTimer = 0.5f + Phase * 0.35f;
                    ExecutePattern(game, toPlayer);
                    AttackCd = 1.5f - Phase * 0.35f;
                }
                break;
            case 2:
                StateTimer -= dt;
                if (dist > 90f)
                {
                    Vel += GameMath.Normalized(toPlayer) * 200f * dt;
                    Vel = GameMath.ClampLength(Vel, Speed + Phase * 12f);
                }
                if (StateTimer <= 0f)
                {
                    State = 0;
                    StateTimer = 0.45f;
                }
                break;
            default:
                StateTimer -= dt;
                Vel *= MathF.Max(0f, 1f - 5f * dt);

                // Король при низком здоровье уходит телепортом - игрок не может
                // прижать его в углу. Голем, наоборот, ускоряется к фазе.
                if (Boss == BossKind.RotKing && Hp < MaxHp * 0.4f && ShockCooldown <= 0f)
                {
                    TeleportNearPlayer(game);
                }
                ShockCooldown = MathF.Max(0f, ShockCooldown - dt);

                if (AttackCd <= 0f && StateTimer <= 0f)
                {
                    State = 1;
                    StateTimer = 0.7f;
                    game.Effects.Ring(Pos, 40f, Tint, 0.5f, 3f);
                }
                break;
        }
    }

    private void ExecutePattern(Game game, Vector2 toPlayer)
    {
        // У каждого босса свой набор атак: ротация общая, но содержание разное.
        switch (Boss)
        {
            case BossKind.RotKing: ExecuteRotKing(game, toPlayer); return;
            case BossKind.AshGolem: ExecuteAshGolem(game, toPlayer); return;
        }

        int pattern = PatternIndex++ % 4;
        if (Phase == 0 && pattern == 2) pattern = 0;
        switch (pattern)
        {
            case 0:
            {
                int bullets = 12 + (int)Phase * 4;
                float baseAngle = MathF.Atan2(toPlayer.Y, toPlayer.X);
                for (int i = 0; i < bullets; i++)
                {
                    float a = baseAngle + i * GameMath.Tau / bullets;
                    SpawnBolt(game, GameMath.FromAngle(a) * 108f + Vel * 0.3f, 11f, Elements.Color(Element.Light));
                }
                game.Camera.Add(3f);
                break;
            }
            case 1:
            {
                for (int burst = 0; burst < 3; burst++)
                {
                    float offset = burst * 0.26f;
                    for (int i = 0; i < 8; i++)
                    {
                        float a = AnimTime * 1.6f + offset + i * GameMath.Tau / 8f;
                        SpawnBolt(game, GameMath.FromAngle(a) * 96f, 10f, Elements.Color(Element.Fire));
                    }
                    game.Effects.Ring(Pos, 26f, Elements.Color(Element.Fire), 0.32f, 2f);
                }
                break;
            }
            case 2:
            {
                int count = 2 + (int)Phase;
                for (int i = 0; i < count; i++)
                {
                    Vector2 target = game.Player.Pos + game.Rng.InsideCircle(46f);
                    Vector2 clamped = new(
                        GameMath.Clamp(target.X, 24f, game.Level.PixelSize.X - 24f),
                        GameMath.Clamp(target.Y, 24f, game.Level.PixelSize.Y - 24f));
                    game.Effects.Telegraph(clamped, 30f, 0.85f - Phase * 0.12f, Palette.Rgb(255, 140, 80), 16f, 200f);
                }
                for (int i = 0; i < 1 + (int)Phase; i++)
                {
                    Vector2 p = game.Level.RandomFloorPoints(game.Rng, 1, Pos, 70f).FirstOrDefault();
                    if (p != Vector2.Zero)
                    {
                        Enemy add = Create(EnemyKind.Slime, p, game.Depth, game.Rng);
                        add.SpawnTimer = 0.6f;
                        game.Enemies.Add(add);
                        game.Effects.Ring(p, 18f, Palette.Rgb(120, 230, 170), 0.5f, 2f);
                    }
                }
                break;
            }
            default:
            {
                Vector2 dir = GameMath.Normalized(toPlayer);
                game.Effects.LineTelegraph(Pos, dir, 220f, 26f, 0.9f, Palette.Rgb(255, 110, 90), 18f, 320f, 0.4f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * GameMath.Tau / 6f + AnimTime;
                    SpawnBolt(game, GameMath.FromAngle(a) * 130f, 10f, Elements.Color(Element.Dark));
                }
                break;
            }
        }
    }

    /// <summary>Перенос Короля в точку поодаль от игрока, но не вплотную.</summary>
    private void TeleportNearPlayer(Game game)
    {
        ShockCooldown = 7f;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 candidate = game.Level.ExitPos + game.Rng.InsideCircle(180f);
            candidate = new Vector2(
                GameMath.Clamp(candidate.X, 24f, game.Level.PixelSize.X - 24f),
                GameMath.Clamp(candidate.Y, 24f, game.Level.PixelSize.Y - 24f));
            if (game.Level.SolidAt(candidate)) continue;
            if (Vector2.Distance(candidate, game.Player.Pos) < 90f) continue;

            game.Effects.Burst(Pos, BossCrystal, 20, 140f, 3f, 0.45f);
            Pos = candidate;
            game.Effects.Ring(Pos, 34f, BossCrystal, 0.6f, 3f, true);
            Vel = Vector2.Zero;
            AudioSystem.PlayAt(Sfx.BossPhase, Pos, game.Player.Pos, 0.55f);
            return;
        }
    }

    /// <summary>
    /// Гнилой Король: держит костяную стражу призывом, сыплет спиралью шипов,
    /// телепортируется на низком здоровье и в финальной фазе вешает проклятие.
    /// </summary>
    private void ExecuteRotKing(Game game, Vector2 toPlayer)
    {
        Color bone = GameMath.Rgb(214, 208, 182);
        Color rot = GameMath.Rgb(150, 200, 130);

        switch (PatternIndex++ % 4)
        {
            case 0:
            {
                // Спираль костяных шипов вокруг босса.
                int arms = 3;
                for (int arm = 0; arm < arms; arm++)
                {
                    float offset = arm * (GameMath.Tau / arms) + AnimTime * 0.9f;
                    for (int i = 0; i < 7; i++)
                    {
                        float a = offset + i * 0.42f;
                        Vector2 at = Pos + GameMath.FromAngle(a) * (34f + i * 15f);
                        game.Effects.Telegraph(at, 11f, 0.7f - i * 0.04f, bone, 12f, 130f);
                    }
                }
                game.Effects.Ring(Pos, 46f, bone, 0.4f, 2f);
                break;
            }

            case 1:
            {
                // Призыв стражи: держит двух-трёх скелетов рядом с собой.
                List<Vector2> spots = game.Level.RandomFloorPoints(game.Rng, 2 + (int)Phase, Pos, 64f);
                foreach (Vector2 p in spots)
                {
                    if (p == Vector2.Zero) continue;
                    Enemy add = Create(EnemyKind.Knight, p, game.Depth, game.Rng);
                    add.Tint = rot;
                    add.SpawnTimer = 0.5f;
                    add.MaxHp = add.Hp *= 0.7f;
                    game.Enemies.Add(add);
                    game.Effects.Ring(p, 20f, rot, 0.55f, 2f);
                }
                AudioSystem.PlayAt(Sfx.Telegraph, Pos, game.Player.Pos, 0.5f);
                break;
            }

            case 2:
            {
                // Рой: веер костяных осколков веером в сторону игрока.
                Vector2 dir = GameMath.Normalized(toPlayer);
                float baseAngle = MathF.Atan2(dir.Y, dir.X);
                int shots = 9 + (int)Phase * 3;
                for (int i = 0; i < shots; i++)
                {
                    float a = baseAngle + (i - shots / 2) * 0.19f;
                    SpawnBolt(game, GameMath.FromAngle(a) * 118f, 11f, bone);
                }
                break;
            }

            default:
            {
                // Проклятие: три круга вокруг игрока - если он в них, получает замедление.
                for (int i = 0; i < 3; i++)
                {
                    Vector2 at = game.Player.Pos + game.Rng.InsideCircle(38f);
                    at = new Vector2(
                        GameMath.Clamp(at.X, 24f, game.Level.PixelSize.X - 24f),
                        GameMath.Clamp(at.Y, 24f, game.Level.PixelSize.Y - 24f));
                    game.Effects.Telegraph(at, 26f, 0.9f - Phase * 0.12f, rot, 14f, 180f);
                    if (Phase >= 1 && Vector2.Distance(game.Player.Pos, at) < 26f)
                    {
                        game.Player.SlowFactor = 0.45f;
                        game.Player.SlowTimer = 2.2f;
                    }
                }
                break;
            }
        }
    }

    /// <summary>
    /// Пепельный Голем: тяжёлый и медленный. Бьёт навесом, оставляет лавовые лужи,
    /// а на низком здоровье заливает арену огнём и трясётся от ударов о стены.
    /// </summary>
    private void ExecuteAshGolem(Game game, Vector2 toPlayer)
    {
        Color lava = GameMath.Rgb(255, 140, 60);
        Color hot = GameMath.Rgb(255, 214, 130);

        switch (PatternIndex++ % 4)
        {
            case 0:
            {
                // Горящий кулак: широкий конус с телеграфом.
                Vector2 dir = GameMath.Normalized(toPlayer);
                game.Effects.LineTelegraph(Pos, dir, 190f, 42f, 1.0f, lava, 22f, 380f, 0.5f);
                for (int i = 0; i < 5; i++)
                {
                    float a = MathF.Atan2(dir.Y, dir.X) + (i - 2) * 0.13f;
                    SpawnBolt(game, GameMath.FromAngle(a) * 142f, 14f, hot);
                }
                break;
            }

            case 1:
            {
                // Лавовые лужи по всему залу - на них больно стоять.
                int pools = 4 + (int)Phase * 2;
                List<Vector2> spots = game.Level.RandomFloorPoints(game.Rng, pools, game.Player.Pos, 40f);
                foreach (Vector2 p in spots)
                {
                    if (p == Vector2.Zero) continue;
                    game.Effects.Field(p, new SpellDef
                    {
                        Name = "лава",
                        Element = Element.Fire,
                        Kind = SpellKind.Field,
                        Color = lava,
                        Radius = 20f,
                        Duration = 4.5f,
                    }, 20f, 4.5f, 12f, 0f, 0f, 0f, 0f);
                }
                break;
            }

            case 2:
            {
                // Толчок: удар оземь с волной во все стороны.
                for (int i = 0; i < 14; i++)
                {
                    float a = i * GameMath.Tau / 14f + AnimTime;
                    SpawnBolt(game, GameMath.FromAngle(a) * 96f, 10f, lava);
                }
                game.Effects.Ring(Pos, 70f, hot, 0.5f, 4f, true);
                game.Camera.Add(6f);
                if (Vector2.Distance(Pos, game.Player.Pos) < 70f)
                {
                    game.Player.TakeDamage(game, 12f, Pos);
                }
                break;
            }

            default:
            {
                // Финальная фаза: арена заливается огнём слоями.
                int rows = 3 + (int)Phase;
                for (int row = 0; row < rows; row++)
                {
                    Vector2 dir = GameMath.Normalized(toPlayer);
                    float len = 260f + row * 40f;
                    game.Effects.LineTelegraph(
                        Pos - dir * (30f + row * 26f), dir, len, 30f,
                        1.1f + row * 0.15f, row % 2 == 0 ? lava : hot, 20f, 420f, 0.4f);
                }
                AudioSystem.PlayAt(Sfx.BossPhase, Pos, game.Player.Pos, 0.9f);
                break;
            }
        }
    }

    private void Shoot(Game game, int count, float spread, float speed, float damage, Color color)
    {
        Vector2 dir = GameMath.Normalized(game.Player.Pos - Pos);
        for (int i = 0; i < count; i++)
        {
            float a = count == 1 ? 0f : (i / (float)(count - 1) - 0.5f) * spread;
            SpawnBolt(game, GameMath.Rotate(dir, a) * speed, damage, color);
        }
    }

    private void SpawnBolt(Game game, Vector2 vel, float damage, Color color)
    {
        Projectile p = new()
        {
            Pos = Pos + GameMath.Normalized(vel) * (Radius + 3f),
            Vel = vel,
            Damage = damage,
            Color = color,
            Element = Element.Light,
            Hostile = true,
            Radius = 4f,
            Size = 4f,
            Life = 5f,
            Style = ProjectileStyle.Orb,
        };
        game.Projectiles.Add(p);
        game.Effects.Burst(p.Pos, color, 4, 60f, 2f, 0.2f);
    }

    /// <summary>
    /// Общий язык отрисовки обычных врагов: тёмный контур, дыхание и вспышка.
 /// Раньше у каждого вида был свой код, и силуэты не читались как единый стиль.
 /// </summary>
    private void DrawSprite(Graphics g, Sprite sprite, float dx, float dy, bool flip, float alpha, float flash)
    {
   // Дыхание: едва заметное изменение размера, чтобы враг жил.
        float breathe = 1f + 0.045f * MathF.Sin(AnimTime * 3.2f + Wave);
   float cx = dx + sprite.Width * 0.5f;
        float cy = dy + sprite.Height * 0.5f;

// Контур: тёмный силуэт, расширенный на пиксель во все стороны.
        // Он отделяет врага от пола и от собственного свечения.
      DrawOutlined(g, sprite, cx, cy, breathe, flip, GameMath.Rgb(6, 5, 12), 0.8f * alpha);

  if (flash > 0.05f)
        {
   DrawOutlined(g, sprite, cx, cy, breathe, flip, Color.White, flash * alpha);
     }
        else
   {
sprite.Draw(g, cx - sprite.Width * 0.5f * breathe, cy - sprite.Height * 0.5f * breathe, flip, breathe);
   }
    }

/// <summary>
    /// Обводка в один пиксель вокруг спрайта. Четыре сдвига вместо восьми:
    /// диагонали дают почти тот же контур, а стоят вдвое дешевле, а их
    /// недоставание не видно на движущемся пиксель-арте.
    /// </summary>
    private static void DrawOutlined(Graphics g, Sprite sprite, float cx, float cy, float scale, bool flip, Color color, float alpha)
    {
        float w = sprite.Width * scale;
        float h = sprite.Height * scale;
        float x = cx - w * 0.5f;
        float y = cy - h * 0.5f;
        sprite.DrawSilhouette(g, x - 1f, y, color, alpha, flip, scale);
        sprite.DrawSilhouette(g, x + 1f, y, color, alpha, flip, scale);
    sprite.DrawSilhouette(g, x, y - 1f, color, alpha, flip, scale);
        sprite.DrawSilhouette(g, x, y + 1f, color, alpha, flip, scale);
    }

    public void Hurt(Game game, float amount, Vector2 from, Element element, float knockback, float stun, float slow = 0f)
    {
        if (Dead || SpawnTimer > 0f) return;
        if (Intangible) return;
        Hp -= amount;
        Flash = 1f;
        if (knockback > 0f)
        {
            Vector2 dir = GameMath.Normalized(Pos - from);
            if (dir.LengthSquared() > 0.01f) Vel += dir * knockback;
        }
        if (stun > 0f && !IsBoss) Stun = MathF.Max(Stun, stun);
        if (slow > 0f)
        {
            SlowFactor = 1f - slow;
            SlowTimer = 2.2f;
        }
        game.Effects.Burst(Pos, Tint, 5, 70f, 2f, 0.25f);
        AudioSystem.PlayAt(Sfx.EnemyHurt, Pos, game.Player.Pos, 0.4f, 0.9f + game.Rng.NextFloat() * 0.35f);
        game.Effects.Label(Pos + new Vector2(game.Rng.Range(-4f, 4f), -Radius - 4f), $"{MathF.Round(amount)}", element == Element.Light ? Palette.Rgb(255, 240, 190) : Color.White);
        if (Hp <= 0f) Die(game);
    }

    private void Die(Game game)
    {
        if (Dead) return;
        Dead = true;
        DeathTimer = 0.32f;
        Hp = 0f;
        game.Kills++;
        game.Score += (int)(XpValue * 2f);
        game.Player.AddMana(game.Player.ManaOnKill);
        if (game.Player.Lifesteal > 0f) game.Player.Heal(game.Player.Lifesteal, false, game);
        game.Effects.Burst(Pos, Tint, 18, 130f, 3f, 0.5f);
        AudioSystem.PlayAt(Sfx.EnemyDeath, Pos, game.Player.Pos, 0.6f, 0.85f + game.Rng.NextFloat() * 0.3f);
        game.Effects.Ring(Pos, Radius * 2.6f, Tint, 0.35f, 2f);

        if (Kind == EnemyKind.Slime)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 offset = game.Rng.InsideCircle(9f);
                Vector2 p = Pos + offset;
                if (game.Level.OverlapsSolid(p.X, p.Y, 4f)) p = Pos;
                Enemy mini = Create(EnemyKind.MiniSlime, p, game.Depth, game.Rng);
                mini.Vel = offset * 3f;
                game.Enemies.Add(mini);
            }
        }

        int xpDrops = Math.Max(1, (int)(XpValue / 5f));
        for (int i = 0; i < xpDrops; i++)
        {
            game.SpawnPickup(PickupKind.Xp, Pos, 4f + XpValue / xpDrops);
        }
        float roll = game.Rng.NextFloat();
        if (roll < 0.3f) game.SpawnPickup(PickupKind.Mana, Pos, 6f);
        else if (roll < 0.36f && game.Player.Hp < game.Player.MaxHp * 0.6f) game.SpawnPickup(PickupKind.Health, Pos, 10f);
    }

    public void Draw(Graphics g, float time)
    {
        if (Dead)
        {
            float a = GameMath.Clamp01(DeathTimer / 0.32f);
            DrawBody(g, a, a * 0.6f, time);
            return;
        }
        if (SpawnTimer > 0f && !IsBoss)
        {
            float t = 1f - SpawnTimer / 0.35f;
            Palette.AddGlow(g, Pos, Radius * 1.9f, Tint, 0.3f);
            Palette.Circle(g, Palette.Fade(Tint, 0.35f), Pos.X, Pos.Y, Radius + (1f - t) * 8f);
            if (t < 0.6f) return;
        }

        float alpha = Intangible ? 0.45f : 1f;
        float flash = Flash;
        if (State == 1 && !IsBoss)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin(time * 22f);
            Palette.AddGlow(g, Pos, Radius * 2.2f, Palette.Danger, 0.3f * pulse);
        }
        DrawBody(g, alpha, flash, time);

        if (Hp < MaxHp * 0.999f && !IsBoss)
        {
            float w = Radius * 2.6f;
            float hp01 = GameMath.Clamp01(Hp / MaxHp);
            float y = Pos.Y - Radius - 6f;
            Palette.Fill(g, Palette.Fade(Color.Black, 0.6f), Pos.X - w * 0.5f, y, w, 2f);
            Palette.Fill(g, GameMath.Mix(Palette.Health, Palette.Health, 0f), Pos.X - w * 0.5f + 1f, y + 0.5f, (w - 2f) * hp01, 1f);
        }
    }

private void DrawBody(Graphics g, float alpha, float flash, float time)
       {
    float bob = MathF.Sin(AnimTime * 4f + Wave) * 1.1f;
   float s = Scale;
  bool flip = Kind == EnemyKind.Knight && MathF.Cos(Angle) < 0f;

    Palette.Disc(g, Palette.Fade(Color.Black, 0.38f * alpha), Pos.X, Pos.Y + Radius * 0.8f, Radius * 0.95f);
           if (Tint.A > 0)
 {
    Palette.AddGlow(g, Pos, Radius * 2.1f, Tint, (0.16f + 0.1f * MathF.Sin(time * 3f + Wave)) * alpha);
     }

float x = Pos.X;
        float y = Pos.Y;
   switch (Kind)
        {
      case EnemyKind.Slime:
 case EnemyKind.MiniSlime:
   {
    Sprite sprite = Kind == EnemyKind.Slime ? Sprites.Slime : Sprites.SlimeMini;
         float sq = s;
         float sx = 1f / MathF.Max(0.4f, sq);
             float w = sprite.Width * sx;
            float h = sprite.Height * sq;
    DrawSprite(g, sprite, x - w * 0.5f, y - h * 0.5f + bob, false, alpha, flash);
       break;
          }
            case EnemyKind.Knight:
     {
           Sprite sprite = Sprites.Knight;
     float w = sprite.Width;
      float h = sprite.Height;
       float dx = x - w * 0.5f;
      float dy = y - h + Radius + bob;
                DrawSprite(g, sprite, dx, dy, flip, alpha, flash);
          if (State == 1)
      {
       Vector2 dir = GameMath.FromAngle(TelegraphAngle);
            Palette.Fill(g, Palette.Fade(Palette.Danger, 0.3f), Pos.X, Pos.Y - 0.5f, TelegraphLength, 1f);
      Palette.Fill(g, Palette.Fade(Palette.Danger, 0.85f), Pos.X, Pos.Y - 0.5f, TelegraphLength * GameMath.Clamp01(1f - StateTimer / 0.62f), 1f);
    _ = dir;
           }
     break;
       }
        case EnemyKind.Shade:
            {
   Sprite sprite = Sprites.Shade;
                float w = sprite.Width;
     float h = sprite.Height;
     float dx = x - w * 0.5f;
  float dy = y - h + Radius * 1.6f + bob;
        DrawSprite(g, sprite, dx, dy, false, alpha, flash);
      break;
       }
            case EnemyKind.Sentinel:
            {
      Sprite sprite = Sprites.Sentinel;
          float w = sprite.Width;
      float h = sprite.Height;
  float dx = x - w * 0.5f;
        float dy = y - h + Radius + bob;
         DrawSprite(g, sprite, dx, dy, false, alpha, flash);
   break;
    }
  case EnemyKind.Boss:
     {
     float w = Sprites.BossWidthFor(Boss);
    float h = Sprites.BossHeightFor(Boss);
   float dx = x - w * 0.5f;
         float dy = y - h + Radius + bob;
   Sprites.DrawBoss(g, dx, dy, AnimTime, Phase, flash, BossVariant);
      break;
         }
        }

        if (Intangible)
        {
            Palette.Circle(g, Palette.Fade(Palette.Rgb(200, 170, 255), 0.5f), Pos.X, Pos.Y, Radius + 3f);
        }
    }
}
