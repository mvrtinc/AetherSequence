using System.Text.Json;
using System.Windows.Forms;
using AetherSequence.Entities;

namespace AetherSequence.Core;

internal enum AimMode
{
    Mouse,
    MouseSoftLock,
    AutoTarget,
}

internal enum ScaleMode
{
    Integer,
    Fit,
}

internal sealed class BindingDto
{
    public string Action { get; set; } = string.Empty;

    public List<int> Keys { get; set; } = new();

    public List<int> Buttons { get; set; } = new();
}

internal sealed class SettingsData
{
    public float ComboWindow { get; set; } = 0.17f;
    public float AutoAimDegrees { get; set; } = 35f;
    public bool MouseAutoFire { get; set; } = true;
    public float AutoCastInterval { get; set; } = 0.16f;
    public bool QuickCastEnabled { get; set; } = true;
    public int AimMode { get; set; } = (int)AetherSequence.Core.AimMode.MouseSoftLock;
    public bool LockCursor { get; set; }
    public bool ShowFps { get; set; }
    public float UiScale { get; set; } = 1f;
    public int ScaleMode { get; set; } = (int)AetherSequence.Core.ScaleMode.Integer;
    public bool OwnCrosshair { get; set; } = true;
    public bool ShowTargetLock { get; set; } = true;
    public bool NativeUi { get; set; } = true;
    public bool Minimap { get; set; } = true;

    /// <summary>
    /// Качество графики: 2 - полное (свет и пыль), 1 - без пыли,
    /// 0 - минимум (без света и пыли). Растяжка мира на 1080p сама по
    /// себе стоит около 12 мс, поэтому быстрым машинам этого не хватает.
 /// </summary>
    public int GraphicsQuality { get; set; } = 2;

    /// <summary>Пыль в воздухе (мелкая анимация, заметно съедает кадр).</summary>
    public bool Dust { get; set; } = true;
    public int MasterVolume { get; set; } = 90;
    public int MusicVolume { get; set; } = 55;
    public int SfxVolume { get; set; } = 80;
    public int WindowW { get; set; } = 1280;
    public int WindowH { get; set; } = 720;
    public int WindowX { get; set; } = -1;
  public int WindowY { get; set; } = -1;
    public bool Fullscreen { get; set; }

    /// <summary>Выбранный класс персонажа: 0 - маг, 1 - стрелок.</summary>
    public int PlayerClass { get; set; }

    // Рекорды хранятся по каждому классу отдельно. Раньше числа были
    // одиночными, и прогресс мага со стрелком смешивался в одну цифру,
    // из-за чего сравнивать было нечего. Старые значения при первом
    // запуске переезжают в ячейку мага.
    public List<int> ClassBestDepth { get; set; } = new();
    public List<int> ClassBestScore { get; set; } = new();
    public List<int> ClassBestKills { get; set; } = new();
    public List<int> ClassRuns { get; set; } = new();

    /// <summary>Всего забегов за все времена: сумма по классам.</summary>
    public int RunsCompleted { get; set; }

    // Старые одиночные рекорды. Читаются только для переноса в ClassBest*,
    // но продолжают попадать в файл: если пользователь вернётся к сборке
    // без классов, его прогресс на месте.
    public int BestDepth { get; set; }

    public int BestScore { get; set; }

    public int BestKills { get; set; }

    public List<BindingDto> Bindings { get; set; } = new();
}

internal sealed class Settings
{
    public const float MinUiScale = 1f;
    public const float MaxUiScale = 1.3f;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<InputAction, List<InputBind>> _bindings = new();

    private readonly SettingsData _data;

    private readonly string _path;

    private Settings(SettingsData data, string path)
    {
        _data = data;
        _path = path;
        MigrateRecords();
        ApplyDefaults();
        LoadFromData();
    }

    /// <summary>
    /// Перенос старых рекордов в ячейки классов. До появления классов
    /// глубина, очки и убийства хранились одним числом на файл, и
    /// при первом запуске новой версии эти значения не должны пропасть.
    /// Всё, что было заработано до классов, засчитываем магу.
    /// </summary>
    private void MigrateRecords()
    {
    // Списки пустые только у файлов, записанных старой версией.
        if (_data.ClassBestDepth.Count == 0
 && _data.ClassBestScore.Count == 0
    && _data.ClassBestKills.Count == 0)
        {
    if (_data.BestDepth > 0 || _data.BestScore > 0 || _data.BestKills > 0)
            {
                _data.ClassBestDepth.Add(_data.BestDepth);
         _data.ClassBestScore.Add(_data.BestScore);
  _data.ClassBestKills.Add(_data.BestKills);
            }

      // RunsCompleted переносим в ячейку мага, но само поле остаётся
    // общей суммой, чтобы старые счётчики не задваивались при записи.
      if (_data.ClassRuns.Count == 0) _data.ClassRuns.Add(0);
        }
    }

    public static string FilePath { get; private set; } = string.Empty;

    /// <summary>
    /// Загрузить настройки из конкретного файла. Нужно тесту миграции:
    /// подсовывать файл, записанный прошлой версией игры, удобнее напрямую,
    /// а не подменяя содержимое settings.json рядом с exe.
    /// </summary>
    public static Settings LoadFromFile(string path)
    {
        FilePath = path;
        SettingsData? data = null;
        try
     {
            if (File.Exists(path))
   {
        data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(path), JsonOptions);
       }
   }
        catch
  {
  data = null;
        }
        return new Settings(data ?? new SettingsData(), path);
    }

    public static Settings Load(string? directory = null)
    {
        string dir = directory ?? AppContext.BaseDirectory;
        return LoadFromFile(Path.Combine(dir, "settings.json"));
    }

    public void Save()
    {
        try
        {
            DumpToData();
            File.WriteAllText(_path, JsonSerializer.Serialize(_data, JsonOptions));
        }
        catch
        {
        }
    }

    public float ComboWindow
    {
        get => _data.ComboWindow;
        set => _data.ComboWindow = GameMath.Clamp(value, 0.08f, 0.45f);
    }

    public float AutoAimDegrees
    {
        get => _data.AutoAimDegrees;
        set => _data.AutoAimDegrees = GameMath.Clamp(value, 0f, 90f);
    }

    public bool MouseAutoFire
    {
        get => _data.MouseAutoFire;
        set => _data.MouseAutoFire = value;
    }

    public float AutoCastInterval
    {
        get => _data.AutoCastInterval;
        set => _data.AutoCastInterval = GameMath.Clamp(value, 0.08f, 0.4f);
    }

    public bool QuickCastEnabled
    {
        get => _data.QuickCastEnabled;
        set => _data.QuickCastEnabled = value;
    }

    public AimMode Aim
    {
        get => (AimMode)_data.AimMode;
        set => _data.AimMode = (int)value;
    }

    public bool LockCursor
    {
        get => _data.LockCursor;
        set => _data.LockCursor = value;
    }

    public bool ShowFps
    {
        get => _data.ShowFps;
        set => _data.ShowFps = value;
    }

    public float UiScale
    {
        get => _data.UiScale;
        set => _data.UiScale = GameMath.Clamp(value, MinUiScale, MaxUiScale);
    }

    public ScaleMode Scale
    {
        get => (ScaleMode)_data.ScaleMode;
        set => _data.ScaleMode = (int)value;
    }

    public bool OwnCrosshair
    {
        get => _data.OwnCrosshair;
        set => _data.OwnCrosshair = value;
    }

    public bool ShowTargetLock
    {
        get => _data.ShowTargetLock;
        set => _data.ShowTargetLock = value;
    }

    public bool NativeUi
    {
        get => _data.NativeUi;
        set => _data.NativeUi = value;
    }

    /// <summary>Показывать миникарту этажа в правом верхнем углу.</summary>
    public bool Minimap
    {
        get => _data.Minimap;
        set => _data.Minimap = value;
    }

    /// <summary>
    /// Качество графики: 2 - высокое, 1 - среднее, 0 - низкое.
    /// Растяжка мира на 1080p стоит около 12 мс, поэтому быстрым машинам
    /// этого не хватает и качество приходится понижать.
    /// </summary>
    public int GraphicsQuality
    {
        get => GameMath.ClampI(_data.GraphicsQuality, 0, 2);
        set => _data.GraphicsQuality = GameMath.ClampI(value, 0, 2);
    }

    /// <summary>Пыль в воздухе.</summary>
    public bool Dust
    {
        get => _data.Dust;
        set => _data.Dust = value;
    }

    public int MasterVolume
    {
        get => _data.MasterVolume;
        set => _data.MasterVolume = GameMath.ClampI(value, 0, 100);
    }

    public int MusicVolume
    {
        get => _data.MusicVolume;
        set => _data.MusicVolume = GameMath.ClampI(value, 0, 100);
    }

    public int SfxVolume
    {
        get => _data.SfxVolume;
        set => _data.SfxVolume = GameMath.ClampI(value, 0, 100);
    }

    /// <summary>Выбранный класс персонажа. Читается при старте забега и дуэли.</summary>
    public PlayerClass PlayerClass
    {
      get => CharacterClasses.Clamp(_data.PlayerClass);
        set => _data.PlayerClass = (int)CharacterClasses.Clamp((int)value);
    }

    /// <summary>
    /// Перейти к следующему классу по кругу. Вызывается из главного меню
    /// и из настроек, поэтому живёт здесь, а не в отрисовке.
    /// </summary>
    public PlayerClass CycleClass(int direction)
    {
        PlayerClass next = CharacterClasses.Next(PlayerClass, direction);
        _data.PlayerClass = (int)next;
        return next;
    }

    /// <summary>
    /// Рекорды по классу. Списки могут быть короче числа классов, если
 /// settings.json писала старая версия игры - недостающие ячейки
    /// читаются как ноль, а при первой записи дописываются.
    /// </summary>
    private static int GetAt(List<int> list, int index)
        => index >= 0 && index < list.Count ? Math.Max(0, list[index]) : 0;

    private static void SetAt(List<int> list, int index, int value)
    {
        while (list.Count <= index) list.Add(0);
        list[index] = Math.Max(0, value);
    }

    public int BestDepthOf(PlayerClass c) => GetAt(_data.ClassBestDepth, (int)c);

    public int BestScoreOf(PlayerClass c) => GetAt(_data.ClassBestScore, (int)c);

    public int BestKillsOf(PlayerClass c) => GetAt(_data.ClassBestKills, (int)c);

    public int RunsOf(PlayerClass c) => GetAt(_data.ClassRuns, (int)c);

    public void SetBestDepth(PlayerClass c, int value) => SetAt(_data.ClassBestDepth, (int)c, value);

    public void SetBestScore(PlayerClass c, int value) => SetAt(_data.ClassBestScore, (int)c, value);

    public void SetBestKills(PlayerClass c, int value) => SetAt(_data.ClassBestKills, (int)c, value);

    public void CountRun(PlayerClass c)
    {
        SetAt(_data.ClassRuns, (int)c, RunsOf(c) + 1);
        _data.RunsCompleted = RunsCompleted + 1;
    }

    /// <summary>Лучший результат по всем классам - для сводки в меню.</summary>
    public int BestDepthAnywhere
    {
        get
        {
            int best = 0;
            for (int i = 0; i < CharacterClasses.Count; i++)
                best = Math.Max(best, BestDepthOf((PlayerClass)i));
            return best;
        }
    }

    public int BestScoreAnywhere
    {
        get
     {
            int best = 0;
            for (int i = 0; i < CharacterClasses.Count; i++)
                best = Math.Max(best, BestScoreOf((PlayerClass)i));
         return best;
    }
    }

    /// <summary>Лучший результат по глубине выбранного класса.</summary>
    public int BestDepth => BestDepthOf(PlayerClass);

    /// <summary>Лучший результат по очкам выбранного класса.</summary>
    public int BestScore => BestScoreOf(PlayerClass);

    /// <summary>Больше всего убийств за один забег выбранного класса.</summary>
    public int BestKills => BestKillsOf(PlayerClass);

    /// <summary>Сколько забегов доведено до финала (победа или смерть).</summary>
    public int RunsCompleted => _data.RunsCompleted;

    /// <summary>
    /// Служебные сеттеры рекордов для тестов: пишут в ячейку класса, минуя
/// игровой путь. Нужны, чтобы проверить миграцию старого settings.json и
    /// раздельность рекордов без реального прохождения забега.
    /// </summary>
    public void TestSetLegacyRecords(int depth, int score, int kills)
    {
        _data.BestDepth = depth;
        _data.BestScore = score;
        _data.BestKills = kills;
    }

    public int WindowW
    {
        get => _data.WindowW;
        set => _data.WindowW = Math.Max(640, value);
    }

    public int WindowH
    {
        get => _data.WindowH;
        set => _data.WindowH = Math.Max(360, value);
    }

    public int WindowX
    {
        get => _data.WindowX;
        set => _data.WindowX = value;
    }

    public int WindowY
    {
        get => _data.WindowY;
        set => _data.WindowY = value;
    }

    public bool Fullscreen
    {
        get => _data.Fullscreen;
        set => _data.Fullscreen = value;
    }

    public IReadOnlyList<InputBind> Binding(InputAction action)
        => _bindings.TryGetValue(action, out List<InputBind>? list) ? list : Array.Empty<InputBind>();

    public bool IsBound(InputAction action, InputBind InputBind) => Binding(action).Contains(InputBind);

    public bool IsKeyBound(Keys key)
    {
        foreach (List<InputBind> list in _bindings.Values)
        {
            foreach (InputBind InputBind in list)
            {
                if (!InputBind.IsMouse && InputBind.Key == key) return true;
            }
        }
        return false;
    }

    public void SetBinding(InputAction action, InputBind InputBind)
    {
        List<InputBind> list = _bindings[action];
        list.Clear();
        if (!InputBind.IsEmpty) list.Add(InputBind);
    }

    public void ToggleBinding(InputAction action, InputBind InputBind)
    {
        List<InputBind> list = _bindings[action];
        if (list.Remove(InputBind)) return;
        list.Add(InputBind);
    }

    public string BindingLabel(InputAction action)
    {
        List<InputBind> list = _bindings[action];
        if (list.Count == 0) return "-";
        string[] parts = new string[list.Count];
        for (int i = 0; i < list.Count; i++) parts[i] = list[i].Label();
        return string.Join(" / ", parts);
    }

    public void ResetBindings()
    {
        ApplyDefaults();
        Save();
    }

    public void ResetAll()
    {
        SettingsData fresh = new();
        fresh.WindowW = _data.WindowW;
        fresh.WindowH = _data.WindowH;
        fresh.WindowX = _data.WindowX;
        fresh.WindowY = _data.WindowY;
        fresh.Fullscreen = _data.Fullscreen;
        // Класс - не настройка внешнего вида, а часть того, кто играет.
        // Сброс "всех настроек" не должен менять выбранного героя, как не
        // меняет он и положение окна.
        fresh.PlayerClass = _data.PlayerClass;
        _data.ComboWindow = fresh.ComboWindow;
        _data.AutoAimDegrees = fresh.AutoAimDegrees;
        _data.MouseAutoFire = fresh.MouseAutoFire;
        _data.AutoCastInterval = fresh.AutoCastInterval;
        _data.QuickCastEnabled = fresh.QuickCastEnabled;
        _data.AimMode = fresh.AimMode;
        _data.LockCursor = fresh.LockCursor;
        _data.ShowFps = fresh.ShowFps;
        _data.UiScale = fresh.UiScale;
        _data.ScaleMode = fresh.ScaleMode;
        _data.OwnCrosshair = fresh.OwnCrosshair;
        _data.ShowTargetLock = fresh.ShowTargetLock;
        _data.NativeUi = fresh.NativeUi;
        _data.Minimap = fresh.Minimap;
  _data.GraphicsQuality = fresh.GraphicsQuality;
        _data.Dust = fresh.Dust;
   _data.MasterVolume = fresh.MasterVolume;
        _data.MusicVolume = fresh.MusicVolume;
        _data.SfxVolume = fresh.SfxVolume;
        ApplyDefaults();
        Save();
    }

    private void ApplyDefaults()
    {
        _bindings.Clear();
        Set(InputAction.MoveUp, Keys.W, Keys.Up);
        Set(InputAction.MoveDown, Keys.S, Keys.Down);
        Set(InputAction.MoveLeft, Keys.A, Keys.Left);
        Set(InputAction.MoveRight, Keys.D, Keys.Right);
        Set(InputAction.Dash, new InputBind(MouseButton.Right), Keys.Space, Keys.ShiftKey, Keys.Z);
        Set(InputAction.Cast1, Keys.D1);
        Set(InputAction.Cast2, Keys.D2);
        Set(InputAction.Cast3, Keys.D3);
        Set(InputAction.Cast4, Keys.D4);
        Set(InputAction.Cast5, Keys.D5);
        Set(InputAction.Cast6, Keys.D6);
        Set(InputAction.Fire, new InputBind(MouseButton.Left));
        Set(InputAction.QuickCast, new InputBind(MouseButton.Middle), Keys.E);
        Set(InputAction.ClearCombo, Keys.X, Keys.Back);
        Set(InputAction.Pause, Keys.Escape, Keys.P);
        Set(InputAction.Options, Keys.O, Keys.F1);
        Set(InputAction.Confirm, Keys.Enter, new InputBind(MouseButton.Left));
        Set(InputAction.NextRune, Keys.Q);
        Set(InputAction.PrevRune, Keys.C);

        // Активация кристалла. Раньше зарядка шла по кнопке атаки (ЛКМ), и это
        // мешало: чтобы зажечь кристалл, приходилось жать то же самое, что и
        // для выстрела. Отдельная клавиша F снимает это неудобство.
        Set(InputAction.ChargeCrystal, Keys.F);
    }

    private void Set(InputAction action, params InputBind[] inputs)
    {
        List<InputBind> list = new();
        foreach (InputBind InputBind in inputs) list.Add(InputBind);
        _bindings[action] = list;
    }

    private void LoadFromData()
    {
        if (_data.Bindings.Count == 0) return;
        foreach (BindingDto dto in _data.Bindings)
        {
            if (!Enum.TryParse(dto.Action, out InputAction action)) continue;
            if (!_bindings.ContainsKey(action)) continue;
            List<InputBind> list = new();
            foreach (int key in dto.Keys)
            {
                if (Enum.IsDefined(typeof(Keys), key)) list.Add(new InputBind((Keys)key));
            }
            foreach (int button in dto.Buttons)
            {
                if (Enum.IsDefined(typeof(MouseButton), button)) list.Add(new InputBind((MouseButton)button));
            }
            _bindings[action] = list;
        }
    }

    private void DumpToData()
    {
        _data.Bindings.Clear();
        foreach ((InputAction action, List<InputBind> list) in _bindings)
        {
            BindingDto dto = new() { Action = action.ToString() };
            foreach (InputBind InputBind in list)
            {
                if (InputBind.IsMouse) dto.Buttons.Add((int)InputBind.Button);
                else dto.Keys.Add((int)InputBind.Key);
            }
            _data.Bindings.Add(dto);
        }
    }
}
