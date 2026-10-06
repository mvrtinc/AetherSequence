using System.Text.Json;
using System.Windows.Forms;

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
    public int BestDepth { get; set; }
    public int BestScore { get; set; }
    public int BestKills { get; set; }
    public int RunsCompleted { get; set; }
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
        ApplyDefaults();
        LoadFromData();
    }

    public static string FilePath { get; private set; } = string.Empty;

    public static Settings Load(string? directory = null)
    {
        string dir = directory ?? AppContext.BaseDirectory;
        string path = Path.Combine(dir, "settings.json");
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

    /// <summary>Самая глубокая достигнутая глубина за все забеги.</summary>
    public int BestDepth
    {
        get => _data.BestDepth;
        set => _data.BestDepth = Math.Max(0, value);
    }

    /// <summary>Лучший результат по очкам.</summary>
    public int BestScore
    {
        get => _data.BestScore;
        set => _data.BestScore = Math.Max(0, value);
    }

    /// <summary>Больше всего убийств за один забег.</summary>
    public int BestKills
    {
        get => _data.BestKills;
        set => _data.BestKills = Math.Max(0, value);
    }

    /// <summary>Сколько забегов доведено до финала (победа или смерть).</summary>
    public int RunsCompleted
    {
        get => _data.RunsCompleted;
        set => _data.RunsCompleted = Math.Max(0, value);
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
