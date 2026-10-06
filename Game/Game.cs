using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using AetherSequence.Art;
using AetherSequence.Audio;
using AetherSequence.Combat;
using AetherSequence.Core;
using AetherSequence.Entities;
using AetherSequence.Net;
using AetherSequence.Progression;
using AetherSequence.World;

namespace AetherSequence;

internal enum GameState
{
    Title,
    Playing,
    LevelUp,
    Paused,
    Settings,
    GameOver,
    Victory,
    Duel,
}

internal static class TitleMenu
{
    public enum Row
    {
        Start,
        Duel,
        Settings,
        Quit,
    }

    public static readonly string[] Labels = { "НАЧАТЬ ЗАБЕГ", "ДУЭЛЬ", "НАСТРОЙКИ", "ВЫХОД" };

    public const int Count = 4;
}

internal enum ConfirmKind
{
    None,
    ExitToMenu,
}

internal static class SettingsMenu
{
    public enum Row
    {
        ComboWindow,
        AutoAim,
        AutoFire,
        AutoInterval,
        AimMode,
        QuickCast,
        OwnCrosshair,
        ShowLock,
        LockCursor,
        UiScale,
        ScaleMode,
        ShowFps,
        NativeUi,
     Minimap,
        Graphics,
        MasterVolume,
        MusicVolume,
        SfxVolume,
        Keys,
        ResetAll,
        Back,
    }

    public static readonly string[] Labels =
    {
        "ОКНО КОМБО",
        "АВТОНАВЕДЕНИЕ",
        "АВТООГОНЬ ЛКМ",
        "ИНТЕРВАЛ ОГНЯ",
        "РЕЖИМ ПРИЦЕЛА",
        "ПОВТОР ЗАКЛИНАНИЯ",
        "СВОЙ ПРИЦЕЛ",
        "МЕТКА ЦЕЛИ",
        "КУРСОР В ОКНЕ",
        "РАЗМЕР ИНТЕРФЕЙСА",
        "МАСШТАБ ОКНА",
        "ПОКАЗАТЬ FPS",
        "ИНТЕРФЕЙС 1920x1080",
"МИНИКАРТА",
        "КАЧЕСТВО ГРАФИКИ",
"ОБЩАЯ ГРОМКОСТЬ",
        "ГРОМКОСТЬ МУЗЫКИ",
        "ГРОМКОСТЬ ЗВУКОВ",
        "КЛАВИШИ И МЫШЬ...",
        "СБРОСИТЬ ВСЁ",
        "НАЗАД",
    };

    public static readonly (InputAction Action, string Label)[] BindingRows =
    {
        (InputAction.MoveUp, "ВВЕРХ"),
        (InputAction.MoveDown, "ВНИЗ"),
        (InputAction.MoveLeft, "ВЛЕВО"),
        (InputAction.MoveRight, "ВПРАВО"),
        (InputAction.Dash, "РЫВОК"),
        (InputAction.Fire, "КАСТ"),
        (InputAction.Cast1, "РУНА 1 (ОГОНЬ)"),
        (InputAction.Cast2, "РУНА 2 (ВЕТЕР)"),
        (InputAction.Cast3, "РУНА 3 (ВОДА)"),
        (InputAction.Cast4, "РУНА 4 (ЗЕМЛЯ)"),
        (InputAction.Cast5, "РУНА 5 (ТЬМА)"),
        (InputAction.Cast6, "РУНА 6 (СВЕТ)"),
        (InputAction.QuickCast, "ПОВТОР ЗАКЛИН."),
        (InputAction.ClearCombo, "СБРОС КОМБО"),
        (InputAction.NextRune, "СЛЕД. РУНА"),
        (InputAction.PrevRune, "ПРЕД. РУНА"),
    };

    public static readonly (InputAction Action, string Label)[] BindingRows2 =
    {
        (InputAction.Pause, "ПАУЗА"),
        (InputAction.Options, "НАСТРОЙКИ"),
        (InputAction.Confirm, "ПОДТВЕРДИТЬ"),
    };

    public static string Value(Settings settings, Row row) => row switch
    {
        Row.ComboWindow => $"{settings.ComboWindow * 1000f:0} мс",
        Row.AutoAim => settings.AutoAimDegrees <= 0f ? "ВЫКЛ" : $"{settings.AutoAimDegrees:0}°",
        Row.AutoFire => settings.MouseAutoFire ? "ВКЛ" : "ВЫКЛ",
        Row.AutoInterval => $"{settings.AutoCastInterval * 1000f:0} мс",
        Row.AimMode => settings.Aim switch
        {
            AimMode.Mouse => "ТОЛЬКО МЫШЬ",
            AimMode.MouseSoftLock => "МЫШЬ + ЗАХВАТ",
            _ => "АВТОЦЕЛЬ",
        },
        Row.QuickCast => settings.QuickCastEnabled ? "ВКЛ" : "ВЫКЛ",
        Row.OwnCrosshair => settings.OwnCrosshair ? "ВКЛ" : "ВЫКЛ",
        Row.ShowLock => settings.ShowTargetLock ? "ВКЛ" : "ВЫКЛ",
        Row.LockCursor => settings.LockCursor ? "ВКЛ" : "ВЫКЛ",
        Row.UiScale => $"{settings.UiScale * 100f:0}%",
        Row.ScaleMode => settings.Scale == ScaleMode.Integer ? "ЦЕЛЫЙ" : "ПО ОКНУ",
        Row.ShowFps => settings.ShowFps ? "ВКЛ" : "ВЫКЛ",
        Row.NativeUi => settings.NativeUi ? "НАТИВНЫЙ" : "ПИКСЕЛЬНЫЙ",
        Row.Minimap => settings.Minimap ? "ВКЛ" : "ВЫКЛ",
     Row.Graphics => settings.GraphicsQuality >= 2 ? "ВЫСОКОЕ" : settings.GraphicsQuality == 1 ? "СРЕДНЕЕ" : "НИЗКОЕ",
        Row.MasterVolume => $"{settings.MasterVolume}%",
        Row.MusicVolume => settings.MusicVolume == 0 ? "ВЫКЛ" : $"{settings.MusicVolume}%",
        Row.SfxVolume => settings.SfxVolume == 0 ? "ВЫКЛ" : $"{settings.SfxVolume}%",
        _ => "",
    };
}

internal sealed class Game
{
    public const int FinalDepth = 15;

    public GameState State = GameState.Title;

    public Settings Settings { get; }

    public GameRenderer Renderer { get; } = new();

    public Input Input { get; }

    public Rng Rng = Rng.FromTime();

    public uint Seed;

    public Level Level { get; set; } = new();

    public Player Player { get; set; } = new();

    /// <summary>Кто сейчас кастует заклинания (в дуэли чередуется).</summary>
    public Player Caster { get; set; } = null!;

    /// <summary>Второй игрок в дуэли (только у хоста).</summary>
    public Player? Foe = null;

    /// <summary>Активен ли режим дуэли.</summary>
    public bool DuelMode = false;

    /// <summary>id локального игрока в дуэли.</summary>
    public byte LocalPeerId = 0;

    /// <summary>Номер тика дуэли - для клиента.</summary>
    public int DuelTick;

    /// <summary>Активная сессия дуэли (null в обычном режиме).</summary>
    public DuelSession? Duel;

    /// <summary>Выбранный пункт титульного меню.</summary>
    public int TitleRow;

    /// <summary>Какое подтверждение сейчас открыто (None - закрыто).</summary>
    public ConfirmKind Confirm = ConfirmKind.None;

    /// <summary>Выбранный пункт в диалоге подтверждения: 0 - да, 1 - отмена.</summary>
    public int ConfirmRow;

    /// <summary>Событие «игрок выбрал выход из игры» - обрабатывается окном.</summary>
    public event Action? RequestQuit;

    /// <summary>
    /// Обновить рекорды, если текущий забег их превзошёл.
    /// Пишет на диск только при реальном улучшении.
    /// </summary>
    public void RecordBest(bool countRun = false)
    {
        bool changed = false;
        if (Depth > Settings.BestDepth) { Settings.BestDepth = Depth; changed = true; }
        if (Score > Settings.BestScore) { Settings.BestScore = Score; changed = true; }
        if (Kills > Settings.BestKills) { Settings.BestKills = Kills; changed = true; }
        if (countRun) { Settings.RunsCompleted++; changed = true; }
        if (changed) Settings.Save();
    }

    /// <summary>Кем является этот процесс в дуэли: 0 - хост, 1 - гость.</summary>
    public byte HostSideId = 0;

    /// <summary>Если задано, локальный игрок дуэли играет не с клавиатуры, а отсюда.</summary>
    public RemoteInputState? DuelLocalOverride;

    public static Player? CurrentHostPlayer;

    public Player? DuelFoeFor(Player caster)
    {
        if (!DuelMode || Foe is null) return null;
        return ReferenceEquals(caster, Player) ? Foe : Player;
    }

    /// <summary>
    /// Кого бьёт снаряд с данным id стрельца. Свой снаряд летит в соперника,
    /// чужой - в меня. Путаница здесь давала «урон самому себе»:
    /// гость кастовал в собственный персонаж и замирал на 3 HP.
    /// </summary>
    public Player? DuelFoeForId(byte peerId)
    {
        if (!DuelMode || Foe is null) return null;
        return peerId == LocalPeerId ? Foe : Player;
    }

    public readonly List<Enemy> Enemies = new();

    public readonly List<Projectile> Projectiles = new();

public readonly List<Pickup> Pickups = new();

    /// <summary>Кристаллы освещения на текущем уровне: по одному на комнату.</summary>
    public readonly List<Crystal> Crystals = new();

/// <summary>Идёт ли сейчас зарядка кристалла. Движение в это время заблокировано.</summary>
    public bool ChargeActive;

    /// <summary>Кристалл, который сейчас заряжается.</summary>
    public Crystal? Charging;

    /// <summary>Короткая блокировка зарядки после удара.</summary>
    private float ChargeBreakLock;

    public readonly EffectSystem Effects = new();

    public readonly Camera Camera = new();

    public int Depth = 1;

    public static bool Autopilot;

    /// <summary>Сразу открыть дуэль против бота при запуске.</summary>
    public static bool AutoStartDuelBot;


    public int Kills;

    public int Score;

    public float Elapsed;

    public float AuraTick;

    public Vector2 ViewSize = new(GameRenderer.Width, GameRenderer.Height);

    public Color FlashColor = Color.White;

    public float FlashAmount;

    public string SpellAnnouncement = string.Empty;

    public float SpellTimer;

    public Color SpellColor = Color.White;

    public bool SpellFusion;

    public string Banner = string.Empty;

    public float BannerTimer;

    public readonly List<CardDef> Choices = new();

    public int ChoiceIndex { get; set; }

    public int PendingLevelUps;

    public float Time;

    public float Fps { get; private set; }

    public Enemy? LockedTarget { get; private set; }

    public int SettingsRow { get; set; }

    public int SettingsPage { get; set; }

    public int RebindingAction { get; set; } = -1;

    public int PauseSelection { get; set; }

    /// <summary>Пауза открылась в этом кадре - остальную обработку надо пропустить.</summary>
    private bool OpenedPauseThisFrame;

    private GameState _settingsReturn = GameState.Playing;

    private MusicTrack _musicTrack = MusicTrack.None;

    public void ApplyAudioSettings()
    {
        AudioSystem.SetVolumes(Settings.MasterVolume / 100f, Settings.MusicVolume / 100f, Settings.SfxVolume / 100f);
    }

    public MusicTrack DesiredMusic() => State switch
    {
        GameState.Title => MusicTrack.Menu,
        GameState.Playing or GameState.LevelUp or GameState.Paused or GameState.Settings => Level.IsBoss ? MusicTrack.Boss : MusicTrack.Dungeon,
        GameState.Victory => MusicTrack.Victory,
        _ => MusicTrack.None,
    };

    private void UpdateMusic()
    {
        MusicTrack desired = Settings.MusicVolume == 0 ? MusicTrack.None : DesiredMusic();
        if (desired == _musicTrack) return;
        _musicTrack = desired;
        AudioSystem.SetMusic(desired);
    }

    private float _fpsAccum;

    private int _fpsFrames;

    public Game(Settings settings)
    {
        Settings = settings;
        Input = new Input(settings);
        Caster = Player;
        Text.SetUiScale(settings.UiScale);
        AudioSystem.Init();
        ApplyAudioSettings();
        _musicTrack = MusicTrack.None;
        UpdateMusic();
        _ = Task.Run(AudioSystem.WarmupBackground);
    }

    public void StartRun(uint seed)
    {
        Seed = seed == 0u ? Rng.FromTime().NextUInt() : seed;
        Rng = new Rng(Seed);
        Player = new Player();
        Caster = Player;
        Depth = 1;
        Kills = 0;
        Score = 0;
        Elapsed = 0f;
        PendingLevelUps = 0;
        Choices.Clear();
        BuildLevel();
        State = GameState.Playing;
        Banner = "ГЛУБИНА 1";
        BannerTimer = 2.2f;
    }

    /// <summary>Начать матч дуэли: пещера, два игрока, три победы до конца.</summary>
    public void StartDuel(byte localId, DuelSession session)
    {
        Duel = session;
        DuelMode = true;
        LocalPeerId = localId;
        HostSideId = session.IsHost ? (byte)0 : (byte)1;
        CurrentHostPlayer = Player;

        Seed = Rng.NextUInt();
        Rng = new Rng(Seed);
        Level = World.Level.GenerateDuelCave(Rng, 1);
        Enemies.Clear();
        Projectiles.Clear();
        Pickups.Clear();
        Effects.Clear();

        Player = new Player();
        Foe = new Player();
        CurrentHostPlayer = Player;

        Player.Teleport(Level.Spawn);
        Foe.Teleport(Level.Spawn2);
        Player.Buffer.Clear();
        Foe.Buffer.Clear();

        Camera.SnapTo(Player.Pos, ViewSize, Level.PixelSize);
        State = GameState.Playing;
        DuelTick = 0;
        Score = 0;
        Kills = 0;
        Elapsed = 0f;
        Banner = "ДУЭЛЬ";
        BannerTimer = 2f;
    }

    /// <summary>Выйти из дуэли обратно в главное меню.</summary>
    public void ExitDuel()
    {
        Duel?.Dispose();
        Duel = null;
        DuelMode = false;
        Foe = null;
        CurrentHostPlayer = null;
        TitleRow = 1;
        State = GameState.Title;
        Level = World.Level.Generate(Rng, 1);
        Player.Teleport(Level.Spawn);
        Camera.SnapTo(Player.Pos, ViewSize, Level.PixelSize);
        UpdateMusic();
    }

    /// <summary>Сброс позиций и здоровья в начале нового раунда.</summary>
    public void ResetDuelRound()
    {
        Projectiles.Clear();
        Effects.Clear();
        Pickups.Clear();

        Player? foe = Foe;
        foreach (Player? p in new Player?[] { Player, foe })
        {
            if (p is null) continue;
            p.Hp = p.MaxHp;
            p.Mana = p.MaxMana;
            p.Alive = true;
            p.HitFlash = 0f;
            p.Invuln = 1.2f;
            p.Vel = Vector2.Zero;
            p.Buffer.Clear();
        }

        Player.Teleport(Level.Spawn);
        foe?.Teleport(Level.Spawn2);
        Camera.SnapTo(Player.Pos, ViewSize, Level.PixelSize);
    }

    /// <summary>Хост: шагнуть симуляцию дуэли на один тик.</summary>
    public void UpdateDuel(float dt)
    {
        if (!DuelMode) return;
        DuelTick++;

        Player local = Player;
        Player? remote = Foe;

        Caster = local;
        UpdateAim();

        if (DuelLocalOverride is { } localState)
        {
            local.AimCamera = Camera.Position;
            local.UpdateRemote(this, localState, dt);
        }
        else
        {
            local.Update(this, dt);
        }
        Caster = local;

        if (Duel is { IsHost: true, Host: not null } session)
        {
            remote!.AimCamera = Camera.Position;
            Caster = remote;
            remote.UpdateRemote(this, session.Host.GetInput((byte)(LocalPeerId ^ 1)), dt);
            Caster = local;
        }

        for (int i = 0; i < Projectiles.Count; i++) CombatUtil.UpdateProjectile(this, Projectiles[i], dt);
        Projectiles.RemoveAll(p => p.Dead);

        Effects.Update(dt, this);
        Camera.Follow(local.Pos, ViewSize, Level.PixelSize, dt);
        FlashAmount = MathF.Max(0f, FlashAmount - dt * 2.2f);
        SpellTimer = MathF.Max(0f, SpellTimer - dt);
        BannerTimer = MathF.Max(0f, BannerTimer - dt);

        // Хост рассылает снапшоты и принимает ввод гостя здесь.
        // Без этого вызова сеть дуэли молчит: соперника не видно,
        // а гость не может двигаться.
        Duel?.Tick(dt);

        CheckDuelRoundEnd();
    }

    private void CheckDuelRoundEnd()
    {
        if (Duel is null) return;
        if (Player.Alive && (Foe is null || Foe.Alive)) return;

        int winner = !Player.Alive && (Foe is null || !Foe.Alive)
            ? -1
            : Player.Alive ? 0 : 1;
        Duel.EndRound(winner);
    }

    public void BuildLevel()
    {
        Level = World.Level.Generate(Rng, Depth);
        Enemies.Clear();
        Projectiles.Clear();
        Pickups.Clear();
        Effects.Clear();
        Player.Teleport(Level.Spawn);
        Player.Buffer.Clear();
        Camera.SnapTo(Player.Pos, ViewSize, Level.PixelSize);
Level.ExitOpen = false;
  LockedTarget = null;
        SpawnEnemies();
  SpawnCrystals();
    }

    /// <summary>
    /// Ставит по кристаллу в каждую комнату обычного уровня. На аренах боссов
    /// кристаллов нет: там один большой зал, он и так освещён целиком.
    /// Позиция берётся из центра комнаты и проверяется на проходимость -
 /// в центре может оказаться колонна.
    /// </summary>
    private void SpawnCrystals()
    {
        Crystals.Clear();
        ChargeActive = false;
   Charging = null;

   if (Level.IsBoss || Level.Rooms.Count == 0) return;

        for (int i = 0; i < Level.Rooms.Count; i++)
  {
   RoomRect room = Level.Rooms[i];
   Vector2 pos = room.CenterPx;

            // Ищем свободную точку рядом с центром, если в центре колонна.
            if (Level.SolidAt(pos))
      {
        List<Vector2> spots = Level.RandomFloorPoints(Rng, 1, room.CenterPx, 0f);
            if (spots.Count == 0) continue;
        pos = spots[0];
            }

       Crystals.Add(new Crystal(pos, i, Rng.Range(0f, GameMath.Tau)));
        }
    }

    /// <summary>
    /// Зарядка кристалла. Игрок стоит на месте, направляет посох и держит F
 /// полторы секунды. Любой урон сбрасывает зарядку - это и есть риск,
    /// который нужно пережить в темноте.
    /// </summary>
    private void UpdateCrystals(float dt)
    {
        // Цель прошлого кадра нужна после обнуления полей: по ней гасим
        // зарядку, если игрок отпустил F, увёл посох или получил урон.
        Crystal? previous = Charging;
        ChargeActive = false;
        Charging = null;
ChargeBreakLock = MathF.Max(0f, ChargeBreakLock - dt);

     for (int i = 0; i < Crystals.Count; i++)
        {
            Crystal c = Crystals[i];
            c.Update(dt);
   }

        if (Crystals.Count == 0) return;

          Player p = Player;
if (!p.Alive || State != GameState.Playing || DuelMode) return;

      // После удара зарядку нельзя тут же продолжить, иначе урон ничего не стоит.
  if (ChargeBreakLock > 0f)
        {
StopCharge(previous);
       return;
    }

        // Кандидат - ближайший незаряженный кристалл в пределах досягаемости.
  Crystal? target = null;
        float best = Crystal.Reach;
        for (int i = 0; i < Crystals.Count; i++)
     {
  Crystal c = Crystals[i];
            if (c.Activated) continue;
        float d = Vector2.Distance(p.Pos, c.Pos);
     if (d < best)
            {
     best = d;
      target = c;
        }
        }

   // Зарядка идёт на F, а не на кнопке атаки: держать ЛКМ, чтобы зажечь
        // кристалл, было неудобно - это же самое, что и выстрел.
    if (target is null || !Input.Down(InputAction.ChargeCrystal))
   {
      // Зарядку прервали: гасим и убираем свечение.
    StopCharge(previous);
      return;
    }

        // Посох должен смотреть в кристалл, иначе зарядка не идёт.
  Vector2 toCrystal = GameMath.Normalized(target.Pos - p.Pos);
    float dot = Vector2.Dot(toCrystal, GameMath.FromAngle(p.AimAngle));
        float aim = MathF.Acos(GameMath.Clamp(dot, -1f, 1f));
     if (aim > 0.5f)
        {
      StopCharge(previous);
     return;
   }

  ChargeActive = true;
        Charging = target;
        target.Charge = MathF.Min(Crystal.ChargeSeconds, target.Charge + dt);
     target.ChargeTime = 0.3f;

        // Чем ближе к готовности, тем гуще частицы - зарядка должна быть видна.
     float k = target.ChargeRatio;
        if (Rng.Chance(dt * (6f + k * 40f)))
      {
        float a = Rng.Range(0f, GameMath.Tau);
    Vector2 around = target.Pos + GameMath.FromAngle(a) * 22f;
  Effects.Burst(around, Palette.ExitOpen, 1, 40f + k * 60f, 1.5f + k, 0.3f, true);
    }

     if (target.Charge >= Crystal.ChargeSeconds) ActivateCrystal(target);
    }

  /// <summary>
    /// Гасит начатую зарядку. Вызывается из урона игроку: держать F под
    /// ударом бессмысленно, кристалл должен начать сначала.
  /// </summary>
    public void BreakCharge()
    {
   if (Charging is null) return;
        StopCharge(Charging);
        ChargeActive = false;
        Charging = null;
        ChargeBreakLock = 0.35f;
    }

    private void StopCharge(Crystal? c)
    {
     if (c is null || c.Activated || c.Charge <= 0f) return;
  c.Reset();
      Effects.Burst(c.Pos, Palette.Muted, 6, 60f, 2f, 0.25f);
    }


    /// <summary>
    /// Активация: вспышка, постоянный свет комнаты и вскрытие на карте.
 /// Враги внутри получают сигнал и переходят в наступление - свет зовёт их.
    /// </summary>
    private void ActivateCrystal(Crystal c)
    {
        c.Activated = true;
        c.Charge = Crystal.ChargeSeconds;
     ChargeActive = false;
      Charging = null;

        // Комната открывается на карте целиком: силуэт и враги внутри
      // становятся видны сразу, без захода в темноту.
        Level.RevealRoom(c.RoomIndex);

        Effects.Ring(c.Pos, 90f, Palette.ExitOpen, 0.9f, 4f, true);
        Effects.Ring(c.Pos, 54f, Color.White, 0.6f, 2f);
     Effects.Burst(c.Pos, Palette.ExitOpen, 34, 180f, 3f, 0.7f);
        Flash(Palette.ExitOpen, 0.3f);
        Camera.Add(5f);
        AudioSystem.Play(Sfx.Portal, 0.6f);

        Banner = "КОМНАТА ОСВЕЩЕНА";
        BannerTimer = 2f;

        // Свет привлекает врагов: из тёмных комнат начинают подтягиваться.
        RouseRoom(c.RoomIndex);
    }

    /// <summary>
    /// Враги в комнате получают цель, а соседние комнаты присылают подкрепление.
/// Раньше враги реагировали только на расстояние, поэтому в темноте они
    /// просто стояли - теперь свет их будит.
    /// </summary>
    private void RouseRoom(int roomIndex)
    {
     if (roomIndex < 0 || roomIndex >= Level.Rooms.Count) return;
        RoomRect room = Level.Rooms[roomIndex];

     int woken = 0;
        for (int i = 0; i < Enemies.Count; i++)
        {
   Enemy e = Enemies[i];
  if (e.Dead) continue;
     if (Level.RoomAt(e.Pos) != roomIndex) continue;

  e.Awakened = true;
  e.AwakenTimer = 12f;
          woken++;
        }

    // Подкрепление из соседних комнат: только те виды, что тянутся к свету.
     List<Vector2> spawns = Level.RandomFloorPoints(Rng, 3, Player.Pos, 170f);
   int added = 0;
        foreach (Vector2 pos in spawns)
   {
      if (added >= 2) break;
     Enemy spawn = Enemy.Create(EnemyKind.MiniSlime, pos, Depth, Rng);
    spawn.Awakened = true;
            spawn.AwakenTimer = 12f;
 Enemies.Add(spawn);
added++;
     }

   }

  private void SpawnEnemies()
    {
        if (Level.IsBoss)
        {
            Vector2 bossPos = Level.ExitPos + new Vector2(0f, 30f);
            Enemies.Add(Enemy.Create(EnemyKind.Boss, bossPos, Depth, Rng));

            // Эскорт берётся из пула темы, а не из фиксированной пары.
            List<EnemyKind> escortPool = LevelThemes.EnemyPool(Depth);
            if (escortPool.Count == 0) escortPool.Add(EnemyKind.Knight);
            List<Vector2> escortPoints = Level.RandomFloorPoints(Rng, 5, Level.Spawn, 140f);
            foreach (Vector2 p in escortPoints)
            {
                Enemies.Add(Enemy.Create(Rng.Pick(escortPool), p, Depth, Rng));
            }

            AudioSystem.Play(Sfx.BossRoar, 0.9f);
            Flash(Level.Colors.ExitOpen, 0.35f);
            return;
        }

        int count = Math.Min(24, 5 + Depth * 2);
        List<Vector2> points = Level.RandomFloorPoints(Rng, count + 8, Level.Spawn, 150f);
        int used = 0;
        for (int i = 0; i < count && used < points.Count; i++)
        {
            Enemies.Add(Enemy.Create(PickKind(), points[used++], Depth, Rng));
        }
    }

    private EnemyKind PickKind()
    {
        // Пул задаёт тема пещеры, но сложность всё равно растёт с глубиной:
        // новые виды открываются только там, где это уже можно пережить.
        List<EnemyKind> pool = LevelThemes.EnemyPool(Depth);
        if (Depth >= 7 && pool.Contains(EnemyKind.Sentinel)) pool.Add(EnemyKind.Sentinel);
        return Rng.Pick(pool);
    }

    public void AnnounceSpell(SpellDef def, bool fusion)
    {
        SpellAnnouncement = def.Name;
        SpellTimer = 1.1f;
        SpellColor = def.Color;
        SpellFusion = fusion;
    }

    public void Flash(Color color, float amount)
    {
        if (amount <= FlashAmount) return;
        FlashColor = color;
        FlashAmount = MathF.Min(0.55f, amount);
    }

    public void SpawnPickup(PickupKind kind, Vector2 pos, float value)
    {
        Pickups.Add(new Pickup
        {
            Pos = pos,
            Vel = Rng.InsideCircle(46f),
            Kind = kind,
            Value = value,
            Wave = Rng.Range(0f, GameMath.Tau),
        });
    }

    public void GainXp(float amount)
    {
        Player.Xp += amount * Player.XpMul;
        while (Player.Xp >= Player.XpToNextLevel && PendingLevelUps < 4)
        {
            Player.Xp -= Player.XpToNextLevel;
            Player.Level++;
            PendingLevelUps++;
            AudioSystem.Play(Sfx.LevelUp, 0.6f);
        }
    }

    public int EnemiesLeft
    {
        get
        {
            int n = 0;
            foreach (Enemy e in Enemies)
            {
                if (!e.Dead) n++;
            }
            return n;
        }
    }

    public Enemy? Boss
    {
        get
        {
            foreach (Enemy e in Enemies)
            {
                if (e.Kind == EnemyKind.Boss && !e.Dead) return e;
            }
            return null;
        }
    }

    public void Update(double dtRaw)
    {
        float dt = (float)Math.Min(dtRaw, 0.05);
        Time += dt;
        Input.Tick(dt);
        UpdateFps(dt);
        UpdateMusic();

// Бой дуэли живёт в State == Playing, а её меню рисуется только в
        // State == Duel. Без этой синхронизации после матча можно застрять
        // в состоянии, где не рисуется ни меню, ни бой.
        if (DuelMode && Duel is not null && State == GameState.Playing
            && Duel.Phase is not (DuelPhase.Fighting or DuelPhase.RoundOver or DuelPhase.MatchOver))
        {
            State = GameState.Duel;
        }

        // Пауза в дуэли: в бою State == Playing, в корневом меню дуэли - State ==
// // Duel, и ни один из этих путей раньше не проверял Pause, поэтому ESC
        // не открывал меню. В комнатах и при вводе IP ESC уже работает как
        // "НАЗАД" (об этом написано в подсказке), там его не перехватываем.
        // На экранах итогов раунда и матча ESC тоже оставлен выходу из дуэли.
        bool duelInFight = DuelMode && Duel is { Phase: DuelPhase.Fighting };
        bool duelMenuRoot = State == GameState.Duel && Duel is { Phase: DuelPhase.Main };
        if (State != GameState.Paused && Confirm == ConfirmKind.None
            && (duelInFight || duelMenuRoot)
            && (Input.Pressed(InputAction.Pause) || Input.RawPressed(Keys.Escape)))
        {
            PauseSelection = 0;
            State = GameState.Paused;
            OpenedPauseThisFrame = true;
        }

        if (OpenedPauseThisFrame)
        {
            // Кадр открытия: UpdatePaused не запускаем, иначе тот же самый
            // ESC тут же закрыл бы только что открытую паузу. EndFrame()
            // в конце Update всё равно очистит нажатие.
            OpenedPauseThisFrame = false;
        }
        else if (Confirm != ConfirmKind.None)
        {
            // Модальный диалог перехватывает весь ввод, пока открыт.
            UpdateConfirm();
        }
        else if (State != GameState.Paused
            && DuelMode && Duel is { Phase: DuelPhase.Fighting or DuelPhase.RoundOver or DuelPhase.MatchOver })
        {
            // Бой дуэли: два игрока, один общий уровень. Обрабатывается
            // отдельно от обычного прохождения, иначе второй игрок
            // превращается в набор случайных событий.
            if (Duel.IsHost) UpdateDuel(dt);
            else TickDuelGuest(dt);

            // Экраны итогов раунда и матча тоже принимают ввод: без этого
            // после смерти игрока было некуда нажать и игра зависала.
            if (Duel is { Phase: DuelPhase.RoundOver or DuelPhase.MatchOver })
            {
                Duel.UpdateInput(Input);
            }
        }
        else
        {
            switch (State)
            {
                case GameState.Title:
                    UpdateTitle();
                    break;
                case GameState.Playing:
                    UpdatePlaying(dt);
                    break;
                case GameState.LevelUp:
                    UpdateLevelUp();
                    break;
                case GameState.Paused:
                    UpdatePaused();
                    break;
                case GameState.Settings:
                    UpdateSettings();
                    break;
                case GameState.GameOver:
                case GameState.Victory:
                    UpdateEndScreen();
                    break;
                case GameState.Duel:
                    UpdateDuelMenu(dt);
                    break;
            }

            if (State != GameState.Duel)
            {
                Effects.Update(dt, this);
                Camera.Update(dt);
                FlashAmount = MathF.Max(0f, FlashAmount - dt * 2.2f);
                SpellTimer = MathF.Max(0f, SpellTimer - dt);
                BannerTimer = MathF.Max(0f, BannerTimer - dt);
            }
        }

        Input.EndFrame();
    }

    /// <summary>
    /// Обновление гостя в дуэли: он не считает физику соперника,
    /// а получает её из сетевых снапшотов.
    /// </summary>
    private void TickDuelGuest(float dt)
    {
        if (Duel is null) return;
        DuelTick++;

        Player local = Player;
        Caster = local;
        UpdateAim();

        // Локального игрока гость считает сам - ответное движение по сети
        // давало бы задержку и управление становилось бы вязким.
        // Если ввод пришёл извне (автопилот тестов), берём его оттуда.
        if (DuelLocalOverride is { Any: true } overrideInput)
        {
            local.AimCamera = Camera.Position;
            local.UpdateRemote(this, overrideInput, dt);
        }
        else
        {
            local.Update(this, dt);
        }
        Caster = local;

        for (int i = 0; i < Projectiles.Count; i++) CombatUtil.UpdateProjectile(this, Projectiles[i], dt);
        Projectiles.RemoveAll(p => p.Dead);

        Effects.Update(dt, this);
        FlashAmount = MathF.Max(0f, FlashAmount - dt * 2.2f);
        SpellTimer = MathF.Max(0f, SpellTimer - dt);
        BannerTimer = MathF.Max(0f, BannerTimer - dt);

        // Позиции обоих приезжают из сети.
        ApplyPeerStates();

        // Итог раунда приходит от хоста событием RoundFinished.
        // Считать его здесь нельзя - счёт удвоится: CheckDuelRoundEnd
        // вызывает EndRound, а та событие тоже увеличивает счёт.
        Duel.Tick(dt);
    }

    /// <summary>Расставляет игроков по последним снапшотам сети.</summary>
    private void ApplyPeerStates()
    {
        if (Duel is null) return;
        foreach (PeerSnapshot snapshot in Duel.RenderPlayers)
        {
            // Свой снапшот игнорируем: локального игрока гость считает сам.
            if (snapshot.Id == Duel.LocalId) continue;
            if (Foe is null) continue;
            Foe.Pos = new Vector2(snapshot.X, snapshot.Y);
            Foe.Hp = snapshot.Hp;
            Foe.MaxHp = snapshot.MaxHp;
            Foe.Mana = snapshot.Mana;
            Foe.AimAngle = snapshot.Aim;
            Foe.Alive = snapshot.Alive;
            Foe.Dashing = snapshot.Dashing;
            Foe.HitFlash = snapshot.HitFlash;
        }

Camera.Follow(Player.Pos, ViewSize, Level.PixelSize, 1f / 60f);
    }

    private void UpdateFps(float dt)
    {
        _fpsAccum += dt;
        _fpsFrames++;
        if (_fpsAccum < 0.5f) return;
        Fps = _fpsFrames / _fpsAccum;
        _fpsAccum = 0f;
        _fpsFrames = 0;
    }

    private void UpdateTitle()
    {
        if (Input.RawPressed(Keys.Up) || Input.RawPressed(Keys.W))
        {
            TitleRow = GameMath.ClampI(TitleRow - 1, 0, TitleMenu.Count - 1);
            AudioSystem.Play(Sfx.UiMove, 0.3f);
        }
        if (Input.RawPressed(Keys.Down) || Input.RawPressed(Keys.S))
        {
            TitleRow = GameMath.ClampI(TitleRow + 1, 0, TitleMenu.Count - 1);
            AudioSystem.Play(Sfx.UiMove, 0.3f);
        }

        bool activate = Input.RawPressed(Keys.Enter) || Input.Pressed(InputAction.Confirm);
        if (!activate && Input.Clicked)
        {
            int hovered = TitleRowAt(Input.Mouse);
            if (hovered >= 0) TitleRow = hovered;
            activate = true;
        }

        if (AutoStartDuelBot)
        {
            // Запуск сразу в тренировку: без второго компьютера.
            AutoStartDuelBot = false;
            State = GameState.Duel;
            Duel = new DuelSession(this);
            Duel.Nick = "МАГ";
            Duel.StartHosting(withBot: true);
            Duel.StartDuelWithBot();
        }

        if (activate)
        {
            AudioSystem.Play(Sfx.UiSelect, 0.5f);
            switch ((TitleMenu.Row)TitleRow)
            {
                case TitleMenu.Row.Start:
                    StartRun(Rng.FromTime().NextUInt());
                    break;
                case TitleMenu.Row.Duel:
                    State = GameState.Duel;
                    Duel = new DuelSession(this);
                    Duel.Phase = DuelPhase.Nickname;
                    Duel.NickDraft = Duel.Nick;
                    Duel.Nick = string.Empty;
                    break;
                case TitleMenu.Row.Settings:
                    OpenSettings();
                    break;
                case TitleMenu.Row.Quit:
                    RequestQuit?.Invoke();
                    break;
            }
            return;
        }

        if (Input.Pressed(InputAction.Options))
        {
            OpenSettings();
        }
    }

    private void UpdateDuelMenu(float dt)
    {
        if (Duel is null)
        {
            Duel = new DuelSession(this);
            Duel.Phase = DuelPhase.Main;
        }
        Duel.Tick(dt);
        Duel.UpdateInput(Input);
    }

    /// <summary>Индекс пункта титульного меню под мышью (или -1).</summary>
    private int TitleRowAt(PointF mouse)
    {
        float y = mouse.Y >= 224f && mouse.Y <= 242f ? -1 : mouse.Y;
        for (int i = 0; i < TitleMenu.Count; i++)
        {
            float itemY = TitleItemY(i);
            if (y >= itemY - 4f && y <= itemY + 16f) return i;
        }
        return -1;
    }

    internal static float TitleItemY(int index) => 250f + index * 24f;

    private void UpdateEndScreen()
    {
        if (State == GameState.GameOver)
        {
            if (Input.RawPressed(Keys.R)) StartRun(Rng.FromTime().NextUInt());
            if (Input.RawPressed(Keys.Enter)) State = GameState.Title;
            if (Input.Pressed(InputAction.Options)) OpenSettings();
        }
        else
        {
            if (Input.RawPressed(Keys.R) || Input.RawPressed(Keys.Enter) || Input.Clicked) StartRun(Rng.FromTime().NextUInt());
        }
    }

    private void UpdatePaused()
    {
        if (Input.Pressed(InputAction.Pause))
        {
            State = GameState.Playing;
            return;
        }
        // В дуэли пункт "НОВЫЙ ЗАБЕГ" не имеет смысла: он сбросил бы
        // одиночный забег вместо возврата в матч, поэтому там на один
        // пункт меньше, а выход в меню сдвинут на строку вверх.
        int pauseLast = DuelMode ? 2 : 3;

        if (Input.RawPressed(Keys.Up) || Input.RawPressed(Keys.W)) { PauseSelection = GameMath.ClampI(PauseSelection - 1, 0, pauseLast); AudioSystem.Play(Sfx.UiMove, 0.3f); }
        if (Input.RawPressed(Keys.Down) || Input.RawPressed(Keys.S)) { PauseSelection = GameMath.ClampI(PauseSelection + 1, 0, pauseLast); AudioSystem.Play(Sfx.UiMove, 0.3f); }
        if (!DuelMode && Input.RawPressed(Keys.R))
        {
            StartRun(Rng.FromTime().NextUInt());
            return;
        }
        if (Input.Pressed(InputAction.Confirm) || Input.Clicked)
        {
            switch (PauseSelection)
            {
                case 0:
                    State = GameState.Playing;
                    break;
                case 1:
                    OpenSettings();
                    break;
                case 2:
                    if (DuelMode)
                    {
                        Confirm = ConfirmKind.ExitToMenu;
                        ConfirmRow = 1;
                        AudioSystem.Play(Sfx.UiMove, 0.3f);
                    }
                    else
                    {
                        StartRun(Rng.FromTime().NextUInt());
                    }
                    break;
                default:
                    Confirm = ConfirmKind.ExitToMenu;
                    ConfirmRow = 1;
                    AudioSystem.Play(Sfx.UiMove, 0.3f);
                    break;
            }
        }
    }

    /// <summary>Модальный диалог подтверждения. Пока открыт - перехватывает весь ввод.</summary>
    private void UpdateConfirm()
    {
        if (Input.RawPressed(Keys.Up) || Input.RawPressed(Keys.W))
        {
            ConfirmRow = GameMath.ClampI(ConfirmRow - 1, 0, 1);
            AudioSystem.Play(Sfx.UiMove, 0.3f);
        }
        if (Input.RawPressed(Keys.Down) || Input.RawPressed(Keys.S))
        {
            ConfirmRow = GameMath.ClampI(ConfirmRow + 1, 0, 1);
            AudioSystem.Play(Sfx.UiMove, 0.3f);
        }
        if (Input.RawPressed(Keys.Escape) || Input.RawPressed(Keys.Back))
        {
            Confirm = ConfirmKind.None;
            AudioSystem.Play(Sfx.UiMove, 0.3f);
            return;
        }
        if (!Input.RawPressed(Keys.Enter) && !Input.Pressed(InputAction.Confirm)) return;

        AudioSystem.Play(Sfx.UiSelect, 0.5f);
        if (Confirm == ConfirmKind.ExitToMenu && ConfirmRow == 0)
        {
            Confirm = ConfirmKind.None;
            ExitToTitle();
        }
        else
        {
            Confirm = ConfirmKind.None;
        }
    }

    /// <summary>Выйти из забега в главное меню: прогресс не сохраняется.</summary>
    public void ExitToTitle()
    {
        RecordBest(countRun: true);
        Confirm = ConfirmKind.None;
        Duel?.Dispose();
        Duel = null;
        DuelMode = false;
        Foe = null;
        CurrentHostPlayer = null;
        Choices.Clear();
        PendingLevelUps = 0;
        Projectiles.Clear();
        Effects.Clear();
        Pickups.Clear();
        PauseSelection = 0;
        TitleRow = 0;
        State = GameState.Title;
        Level = World.Level.Generate(Rng, 1);
        Camera.SnapTo(Player.Pos, ViewSize, Level.PixelSize);
        UpdateMusic();
    }

    private void UpdateSettings()
    {
        if (RebindingAction >= 0)
        {
            if (Input.TryCaptureBinding(out InputBind captured))
            {
                Settings.ToggleBinding((InputAction)RebindingAction, captured);
                Settings.Save();
            }
            if (Input.RawPressed(Keys.Escape) || Input.Pressed(InputAction.Pause))
            {
                RebindingAction = -1;
            }
            return;
        }

        if (Input.RawPressed(Keys.Escape) || Input.Pressed(InputAction.Options) || Input.Pressed(InputAction.Pause))
        {
            Settings.Save();
            AudioSystem.Play(Sfx.UiBack, 0.35f);
            State = _settingsReturn;
            return;
        }

        if (SettingsPage == 0)
        {
            int rows = SettingsMenu.Labels.Length;
            if (Input.RawPressed(Keys.Up) || Input.RawPressed(Keys.W)) SettingsRow = GameMath.ClampI(SettingsRow - 1, 0, rows - 1);
            if (Input.RawPressed(Keys.Down) || Input.RawPressed(Keys.S)) SettingsRow = GameMath.ClampI(SettingsRow + 1, 0, rows - 1);

            bool changed = Input.RawPressed(Keys.Left) || Input.RawPressed(Keys.A)
                || Input.RawPressed(Keys.Right) || Input.RawPressed(Keys.D);
            if (changed)
            {
                AdjustSettings((SettingsMenu.Row)SettingsRow, Input.RawPressed(Keys.Left) || Input.RawPressed(Keys.A) ? -1 : 1);
            }

            if (Input.Pressed(InputAction.Confirm) || Input.Clicked)
            {
                ActivateSettingsRow();
            }
            return;
        }

        int total = SettingsMenu.BindingRows.Length + SettingsMenu.BindingRows2.Length;
        if (Input.RawPressed(Keys.Up) || Input.RawPressed(Keys.W)) SettingsRow = GameMath.ClampI(SettingsRow - 1, 0, total - 1);
        if (Input.RawPressed(Keys.Down) || Input.RawPressed(Keys.S)) SettingsRow = GameMath.ClampI(SettingsRow + 1, 0, total - 1);
        if (Input.RawPressed(Keys.Escape) || (Input.Pressed(InputAction.Confirm) && SettingsRow == total))
        {
            SettingsPage = 0;
            SettingsRow = (int)SettingsMenu.Row.Keys;
        }
        if (Input.Pressed(InputAction.Confirm) || Input.Clicked)
        {
            InputAction action = SettingsRow < SettingsMenu.BindingRows.Length
                ? SettingsMenu.BindingRows[SettingsRow].Action
                : SettingsMenu.BindingRows2[SettingsRow - SettingsMenu.BindingRows.Length].Action;
            RebindingAction = (int)action;
        }
    }

    private void OpenSettings()
    {
        SettingsRow = 0;
        SettingsPage = 0;
        RebindingAction = -1;
        _settingsReturn = State == GameState.Settings ? _settingsReturn : State;
        State = GameState.Settings;
    }

    private void AdjustSettings(SettingsMenu.Row row, int direction)
    {
        switch (row)
        {
            case SettingsMenu.Row.ComboWindow:
                Settings.ComboWindow += direction * 0.02f;
                break;
            case SettingsMenu.Row.AutoAim:
                Settings.AutoAimDegrees = direction < 0
                    ? MathF.Max(0f, Settings.AutoAimDegrees - 10f)
                    : MathF.Min(90f, Settings.AutoAimDegrees + 10f);
                break;
            case SettingsMenu.Row.AutoFire:
                Settings.MouseAutoFire = !Settings.MouseAutoFire;
                break;
            case SettingsMenu.Row.AutoInterval:
                Settings.AutoCastInterval += direction * 0.02f;
                break;
            case SettingsMenu.Row.AimMode:
                Settings.Aim = (AimMode)(((int)Settings.Aim + (direction < 0 ? 2 : 1)) % 3);
                break;
            case SettingsMenu.Row.QuickCast:
                Settings.QuickCastEnabled = !Settings.QuickCastEnabled;
                break;
            case SettingsMenu.Row.OwnCrosshair:
                Settings.OwnCrosshair = !Settings.OwnCrosshair;
                break;
            case SettingsMenu.Row.ShowLock:
                Settings.ShowTargetLock = !Settings.ShowTargetLock;
                break;
            case SettingsMenu.Row.LockCursor:
                Settings.LockCursor = !Settings.LockCursor;
                break;
            case SettingsMenu.Row.UiScale:
                Settings.UiScale = direction < 0
                    ? MathF.Max(Settings.MinUiScale, Settings.UiScale - 0.15f)
                    : MathF.Min(Settings.MaxUiScale, Settings.UiScale + 0.15f);
                Text.SetUiScale(Settings.UiScale);
                break;
            case SettingsMenu.Row.ScaleMode:
                Settings.Scale = Settings.Scale == ScaleMode.Integer ? ScaleMode.Fit : ScaleMode.Integer;
                break;
            case SettingsMenu.Row.ShowFps:
                Settings.ShowFps = !Settings.ShowFps;
                break;
case SettingsMenu.Row.NativeUi:
    Settings.NativeUi = !Settings.NativeUi;
    break;
case SettingsMenu.Row.Minimap:
            Settings.Minimap = !Settings.Minimap;
            break;
            case SettingsMenu.Row.Graphics:
     // 0 - низкое, 1 - среднее, 2 - высокое. Растяжка мира на 1080p
     // стоит около 12 мс, поэтому на слабых машинах качество приходится
    // понижать руками.
   Settings.GraphicsQuality = (Settings.GraphicsQuality + 1) % 3;
  break;
     case SettingsMenu.Row.MasterVolume:
                Settings.MasterVolume = GameMath.ClampI(Settings.MasterVolume + (direction > 0 ? 10 : -10), 0, 100);
                ApplyAudioSettings();
                break;
            case SettingsMenu.Row.MusicVolume:
                Settings.MusicVolume = GameMath.ClampI(Settings.MusicVolume + (direction > 0 ? 10 : -10), 0, 100);
                ApplyAudioSettings();
                if (Settings.MusicVolume > 0) AudioSystem.SetMusic(DesiredMusic());
                break;
            case SettingsMenu.Row.SfxVolume:
                Settings.SfxVolume = GameMath.ClampI(Settings.SfxVolume + (direction > 0 ? 10 : -10), 0, 100);
                ApplyAudioSettings();
                break;
        }
    }

    private void ActivateSettingsRow()
    {
        switch ((SettingsMenu.Row)SettingsRow)
        {
            case SettingsMenu.Row.Keys:
                SettingsPage = 1;
                SettingsRow = 0;
                break;
            case SettingsMenu.Row.ResetAll:
                Settings.ResetAll();
                Text.SetUiScale(Settings.UiScale);
                break;
            case SettingsMenu.Row.Back:
                Settings.Save();
                State = _settingsReturn;
                break;
            default:
                AdjustSettings((SettingsMenu.Row)SettingsRow, 1);
                break;
        }
    }

    private void UpdateLevelUp()
    {
        if (Choices.Count == 0)
        {
            PendingLevelUps = 0;
            State = GameState.Playing;
            return;
        }

        int direct = -1;
        if (Input.Pressed(InputAction.Cast1)) direct = 0;
        if (Input.Pressed(InputAction.Cast2)) direct = 1;
        if (Input.Pressed(InputAction.Cast3)) direct = 2;
        if (direct >= 0)
        {
            ApplyChoice(direct);
            return;
        }

        int selected = ChoiceIndex;
        if (Input.RawPressed(Keys.Left) || Input.RawPressed(Keys.A)) selected = GameMath.ClampI(selected - 1, 0, Choices.Count - 1);
        if (Input.RawPressed(Keys.Right) || Input.RawPressed(Keys.D)) selected = GameMath.ClampI(selected + 1, 0, Choices.Count - 1);
        ChoiceIndex = selected;

        bool clickOnCard = false;
        if (Input.MouseInside && Input.Clicked)
        {
            int hit = CardAt(Input.Mouse);
            if (hit >= 0)
            {
                selected = hit;
                clickOnCard = true;
            }
        }

        if (Input.Pressed(InputAction.Confirm) || clickOnCard)
        {
            ApplyChoice(GameMath.ClampI(selected, 0, Choices.Count - 1));
        }
    }

    private void ApplyChoice(int index)
    {
        if (index >= 0 && index < Choices.Count)
        {
            Choices[index].Apply(Player);
            Effects.Label(Player.Pos + new Vector2(0f, -20f), Choices[index].Name, Choices[index].Color, true, 1.4f);
            AudioSystem.Play(Sfx.CardPick, 0.5f);
        }
        PendingLevelUps = Math.Max(0, PendingLevelUps - 1);
        if (PendingLevelUps > 0) OpenCardChoice();
        else
        {
            Choices.Clear();
            State = GameState.Playing;
        }
    }

    private void OpenCardChoice()
    {
        Choices.Clear();
        List<CardDef> rolled = Cards.Roll(Rng, 3, card =>
        {
            if (card.Name == "Руна Земли") return !Player.RuneUnlocked[(int)Element.Earth];
            if (card.Name == "Руна Света") return !Player.RuneUnlocked[(int)Element.Light];
            return true;
        });
        Choices.AddRange(rolled);
        ChoiceIndex = 0;
        State = GameState.LevelUp;
    }

    public int CardAt(PointF mouse)
    {
        for (int i = 0; i < Choices.Count; i++)
        {
            if (CardRect(i).Contains(mouse)) return i;
        }
        return -1;
    }

    public static RectangleF CardRect(int index)
    {
        const float cardW = 172f;
        const float cardH = 196f;
        const float gap = 14f;
        float totalW = cardW * 3f + gap * 2f;
        float x = GameRenderer.Width * 0.5f - totalW * 0.5f + index * (cardW + gap);
        return new RectangleF(x, 96f, cardW, cardH);
    }

    private void UpdatePlaying(float dt)
    {
        if (Input.Pressed(InputAction.Pause))
        {
            PauseSelection = 0;
            State = GameState.Paused;
            return;
        }
        if (Input.Pressed(InputAction.Options))
        {
            OpenSettings();
            return;
        }

        if (!Player.Alive)
        {
            Player.DeathTimer += dt;
            if (Player.DeathTimer < dt * 1.5f)
            {
                Effects.Burst(Player.Pos, Palette.Rgb(120, 240, 255), 40, 170f, 3.5f, 0.7f);
                Effects.Ring(Player.Pos, 60f, Palette.Rgb(120, 240, 255), 0.7f, 3f);
                Flash(Palette.Danger, 0.5f);
                Camera.Add(7f);
            }
            if (Player.DeathTimer > 1.3f)
            {
                State = GameState.GameOver;
                RecordBest(countRun: true);
                AudioSystem.Play(Sfx.Death, 0.7f);
            }
            return;
        }

        Elapsed += dt;

        if (Input.WheelDelta != 0) Player.CycleRune(Input.WheelDelta > 0 ? 1 : -1);
        if (Input.Pressed(InputAction.NextRune)) Player.CycleRune(1);
        if (Input.Pressed(InputAction.PrevRune)) Player.CycleRune(-1);

UpdateAim();

  // Пока идёт зарядка кристалла, игрок стоит на месте. Сужающийся свет
    // посоха - это и есть цена, которую платишь за решение постоять.
    if (ChargeActive) Player.Vel = Vector2.Zero;

  UpdateCrystals(dt);

  Player.Update(this, dt);

        for (int i = 0; i < Enemies.Count; i++)
        {
            Enemies[i].Update(this, dt);
        }

        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            Projectile pr = Projectiles[i];
            CombatUtil.UpdateProjectile(this, pr, dt);
            if (pr.Dead) Projectiles.RemoveAt(i);
        }

        for (int i = Pickups.Count - 1; i >= 0; i--)
        {
            Pickup pk = Pickups[i];
            pk.Update(dt, Player.Pos, 40f);
            if (!pk.Dead && Vector2.Distance(pk.Pos, Player.Pos) < Player.Radius + 5f)
            {
                Collect(pk);
                pk.Dead = true;
            }
            if (pk.Dead) Pickups.RemoveAt(i);
        }

        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            Enemy e = Enemies[i];
            if (e.Dead && e.DeathTimer <= 0f) Enemies.RemoveAt(i);
        }

        Camera.Follow(Player.Pos, ViewSize, Level.PixelSize, dt);

        CheckLevelState();

        if (PendingLevelUps > 0)
        {
            OpenCardChoice();
        }
    }

    private void UpdateAim()
    {
        LockedTarget = null;
        Player p = Player;

        if (Settings.Aim == AimMode.AutoTarget)
        {
            Enemy? auto = FindTarget(240f, GameMath.Tau, 0f);
            if (auto is not null)
            {
                p.AimAngle = GameMath.AngleOf(auto.Pos - p.Pos);
                LockedTarget = auto;
            }
            return;
        }

        if (!Input.MouseInside) return;

        Vector2 toMouse = new Vector2(Input.Mouse.X, Input.Mouse.Y) + Camera.Position - p.Pos;
        if (toMouse.LengthSquared() < 16f) return;
        float mouseAngle = GameMath.AngleOf(toMouse);

        if (Settings.Aim == AimMode.MouseSoftLock && Settings.AutoAimDegrees > 0f)
        {
            Enemy? target = FindTarget(320f, Settings.AutoAimDegrees * GameMath.DegToRad, mouseAngle);
            if (target is not null)
            {
                p.AimAngle = GameMath.AngleOf(target.Pos - p.Pos);
                LockedTarget = target;
                return;
            }
        }

        p.AimAngle = mouseAngle;
    }

    private Enemy? FindTarget(float range, float coneRad, float centerAngle)
    {
        Enemy? best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < Enemies.Count; i++)
        {
            Enemy e = Enemies[i];
            if (e.Dead || e.Intangible) continue;
            Vector2 d = e.Pos - Player.Pos;
            float dist = d.Length();
            if (dist < 4f || dist > range) continue;
            if (coneRad < GameMath.Tau)
            {
                float angle = GameMath.AngleOf(d);
                float diff = MathF.Abs(MathF.Atan2(MathF.Sin(angle - centerAngle), MathF.Cos(angle - centerAngle)));
                if (diff > coneRad) continue;
            }
            if (!Level.LineClear(Player.Pos, e.Pos)) continue;
            if (dist >= bestDist) continue;
            bestDist = dist;
            best = e;
        }
        return best;
    }

    private void Collect(Pickup pk)
    {
        switch (pk.Kind)
        {
            case PickupKind.Xp:
                GainXp(pk.Value);
                Effects.Burst(pk.Pos, Palette.Xp, 5, 50f, 2f, 0.25f);
                AudioSystem.Play(Sfx.Pickup, 0.3f, 1.15f);
                break;
            case PickupKind.Mana:
                Player.AddMana(pk.Value, false);
                Effects.Burst(pk.Pos, Palette.Mana, 5, 50f, 2f, 0.25f);
                AudioSystem.Play(Sfx.PickupMana, 0.28f, 1.1f);
                break;
            default:
                Player.Heal(pk.Value, true, this);
                Effects.Burst(pk.Pos, Palette.Health, 6, 50f, 2f, 0.25f);
                AudioSystem.Play(Sfx.PickupHealth, 0.4f, 1f);
                break;
        }
    }

    private void CheckLevelState()
    {
        bool cleared = Level.IsBoss ? Boss is null : EnemiesLeft == 0;
        if (cleared && !Level.ExitOpen)
        {
            Level.ExitOpen = true;
            Banner = Level.IsBoss ? "ПУТЬ ОТКРЫТ" : "УРОВЕНЬ ЗАЧИЩЕН";
            BannerTimer = 2f;
            Effects.Ring(Level.ExitPos, 40f, Palette.ExitOpen, 0.8f, 2f);
            AudioSystem.Play(Sfx.Portal, 0.55f);
        }

        if (Level.ExitOpen && Vector2.Distance(Player.Pos, Level.ExitPos) < 15f)
        {
            Descend();
        }
    }

    public void Descend()
    {
        Depth++;
        if (Depth > FinalDepth)
        {
            State = GameState.Victory;
            RecordBest(countRun: true);
            AudioSystem.Play(Sfx.Victory, 0.8f);
            return;
        }
        BuildLevel();
        Player.Mana = Player.MaxMana;
        Player.Invuln = 1f;
        RecordBest();
        Banner = Level.IsBoss ? $"ГЛУБИНА {Depth} — СТРАЖ" : $"ГЛУБИНА {Depth}";
        BannerTimer = 2.4f;
        Flash(Palette.ExitOpen, 0.4f);
    }
}
