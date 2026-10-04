using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Windows.Forms;
using AetherSequence.Audio;
using AetherSequence.Core;

namespace AetherSequence.Forms;

internal sealed class GameForm : Form
{
    private const double Step = 1.0 / 60.0;

    private readonly Settings _settings;
    private readonly Game _game;
    private readonly Bitmap _worldBuffer;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private double _last;
    private double _accumulator;
    private bool _fullscreen;
    private Rectangle _restoreBounds;
    private bool _cursorHidden;

    public GameForm()
    {
        _settings = Settings.Load();
        _game = new Game(_settings);
        _worldBuffer = new Bitmap((int)GameRenderer.Width, (int)GameRenderer.Height, PixelFormat.Format32bppRgb);
        _timer = new System.Windows.Forms.Timer { Interval = 1 };

        Text = "AETHER SEQUENCE";
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.Black;
        MinimumSize = new Size((int)GameRenderer.Width + 16, (int)GameRenderer.Height + 40);
        FormBorderStyle = FormBorderStyle.Sizable;

        int width = _settings.WindowW;
        int height = _settings.WindowH;
        bool firstRun = !File.Exists(Settings.FilePath);

        if (firstRun)
        {
            Rectangle bounds = Screen.FromControl(this).Bounds;
            if (bounds.Width >= 1920 && bounds.Height >= 1080)
            {
                width = 1920;
                height = 1080;
                _settings.Fullscreen = true;
            }
            else
            {
                width = Math.Max(1280, bounds.Width - 80);
                height = Math.Max(720, bounds.Height - 80);
            }
        }

        bool restored = false;
        if (!firstRun && (_settings.WindowX >= -1 || _settings.WindowY >= -1))
        {
            Rectangle wanted = new(_settings.WindowX, _settings.WindowY, width, height);
            bool visible = Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(wanted));
            if (visible && wanted.Width >= MinimumSize.Width && wanted.Height >= MinimumSize.Height)
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = wanted;
                restored = true;
            }
        }

        if (!restored)
        {
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(width, height);
        }

        _timer.Tick += (_, _) => Tick();
        _game.RequestQuit += () => Close();
        Shown += (_, _) =>
        {
            if (_settings.Fullscreen) ApplyFullscreen(true);
            _last = _clock.Elapsed.TotalSeconds;
            UpdateCursor();
            _timer.Start();
        };
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _timer.Dispose();
            _worldBuffer.Dispose();
            AudioSystem.Shutdown();
        };
    }

    private void Tick()
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = now - _last;
        _last = now;
        if (dt <= 0) dt = Step;
        if (dt > 0.25) dt = Step;
        _accumulator += dt;

        int steps = 0;
        while (_accumulator >= Step && steps < 6)
        {
            if (Game.Autopilot) AutoPilot.Step(_game);
            _game.Update(Step);
            _accumulator -= Step;
            steps++;
        }
        if (steps >= 6) _accumulator = 0;

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics screen = e.Graphics;
        _game.ViewSize = new Vector2(GameRenderer.Width, GameRenderer.Height);

        using (Graphics worldGraphics = Graphics.FromImage(_worldBuffer))
        {
            worldGraphics.CompositingMode = CompositingMode.SourceCopy;
            worldGraphics.Clear(Color.Black);
            worldGraphics.CompositingMode = CompositingMode.SourceOver;
            _game.Renderer.DrawWorldOnly(worldGraphics, _game);
        }

ComputeViewport(out float scale, out PointF origin);
  screen.CompositingMode = CompositingMode.SourceOver;
        screen.SmoothingMode = SmoothingMode.None;
        screen.PixelOffsetMode = PixelOffsetMode.Half;

// Мир всегда непрозрачный, поэтому растягиваем его без фильтрации.
        // HighQualityBilinear на растяжке 640x360 -> 1920x1080 стоил 48 мс
        // на кадр - это и было причиной 15-20 FPS. NearestNeighbor стоит
        // около 11 мс, и это минимум для GDI+. Bilinear не использовать:
        // на нём GDI+ медленнее HighQualityBilinear в разы (135 мс).
        //
        // Прямой блит через LockBits пробовал (FastScale): в изоляции он
        // выигрывал, но на живой игре оказался медленнее DrawImage
        // (21.3 против 19.0 мс) - GetDIBits в поверхность окна дороже
        // блита GDI+. Поэтому растяжка остаётся на DrawImage.
        screen.CompositingMode = CompositingMode.SourceCopy;
        screen.InterpolationMode = InterpolationMode.NearestNeighbor;
     screen.CompositingQuality = CompositingQuality.HighSpeed;
        screen.DrawImage(
   _worldBuffer,
         new RectangleF(origin.X, origin.Y, GameRenderer.Width * scale, GameRenderer.Height * scale),
            new RectangleF(0f, 0f, (float)_worldBuffer.Width, (float)_worldBuffer.Height),
       GraphicsUnit.Pixel);
        screen.CompositingMode = CompositingMode.SourceOver;

        // Интерфейс рисуем сразу на окно. Раньше он уходил в полноэкранный
        // буфер с прозрачностью, а потом блитился поверх мира - это ещё
        // 11 мс на кадр только на смешивание альфы впустую.
        _game.Renderer.DrawHudOnly(screen, _game, scale, _settings.NativeUi, origin);
}

    private void ComputeViewport(out float scale, out PointF origin)
    {
        int sw = Math.Max(1, ClientSize.Width);
        int sh = Math.Max(1, ClientSize.Height);
        scale = MathF.Min(sw / GameRenderer.Width, sh / GameRenderer.Height);
        if (scale <= 0f) scale = 1f;
        if (_settings.Scale == ScaleMode.Integer && scale >= 1f)
        {
            scale = MathF.Floor(scale);
        }
        float w = GameRenderer.Width * scale;
        float h = GameRenderer.Height * scale;
        origin = new PointF(MathF.Round((sw - w) * 0.5f), MathF.Round((sh - h) * 0.5f));
    }

    private PointF ToVirtual(Point client)
    {
        ComputeViewport(out float scale, out PointF origin);
        if (scale <= 0f) scale = 1f;
        return new PointF((client.X - origin.X) / scale, (client.Y - origin.Y) / scale);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        if (key == Keys.F11)
        {
            ToggleFullscreen();
            return true;
        }
        if (IsGameKey(key))
        {
            _game.Input.KeyDown(key);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool IsGameKey(Keys key)
    {
        if (key == Keys.None) return false;
        if (_settings.IsKeyBound(key)) return true;
        return key switch
        {
            Keys.W or Keys.A or Keys.S or Keys.D => true,
            Keys.Up or Keys.Down or Keys.Left or Keys.Right => true,
            Keys.Space or Keys.Enter or Keys.Escape or Keys.Back or Keys.Tab => true,
            >= Keys.D0 and <= Keys.D9 => true,
            >= Keys.NumPad0 and <= Keys.NumPad9 => true,
            Keys.Oemplus or Keys.Add or Keys.OemMinus or Keys.Subtract => true,
            Keys.X or Keys.Y or Keys.Z or Keys.C or Keys.V or Keys.R or Keys.P or Keys.Q or Keys.E or Keys.F => true,
            Keys.G or Keys.H or Keys.J or Keys.K or Keys.L or Keys.U or Keys.I or Keys.O or Keys.T => true,
            Keys.N or Keys.M or Keys.B => true,
            Keys.ShiftKey or Keys.ControlKey or Keys.Alt or Keys.LShiftKey or Keys.RShiftKey or Keys.Menu => true,
            _ => false,
        };
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _game.Input.KeyUp(e.KeyCode & Keys.KeyCode);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _game.Input.MouseMove(ToVirtual(e.Location));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _game.Input.MouseMove(ToVirtual(e.Location));
        _game.Input.MouseDown(ToButton(e.Button));
        Focus();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _game.Input.MouseUp(ToButton(e.Button));
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _game.Input.MouseWheel(Math.Sign(e.Delta));
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateCursor();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _game.Input.MouseLeave();
        Cursor.Clip = Rectangle.Empty;
        Cursor.Show();
        _cursorHidden = false;
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        UpdateCursor();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        _game.Input.ClearAll();
        Cursor.Clip = Rectangle.Empty;
        Cursor.Show();
        _cursorHidden = false;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_settings.LockCursor) UpdateCursor();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _game.RecordBest(countRun: StateShouldCountRun());
        _game.Duel?.Dispose();
        _game.Duel = null;
        _settings.Fullscreen = _fullscreen;
        if (!_fullscreen)
        {
            _settings.WindowX = Bounds.Left;
            _settings.WindowY = Bounds.Top;
            _settings.WindowW = Math.Max(Bounds.Width, MinimumSize.Width);
            _settings.WindowH = Math.Max(Bounds.Height, MinimumSize.Height);
        }
        _settings.Save();
        base.OnFormClosing(e);
    }

    /// <summary>Забег засчитывается, только если он реально шёл (а не висел на титуле).</summary>
    private bool StateShouldCountRun()
    {
        GameState state = _game.State;
        return state is GameState.Playing or GameState.LevelUp or GameState.Paused
            or GameState.GameOver or GameState.Victory;
    }


    private static MouseButton ToButton(MouseButtons button) => button switch
    {
        MouseButtons.Left => MouseButton.Left,
        MouseButtons.Right => MouseButton.Right,
        MouseButtons.Middle => MouseButton.Middle,
        _ => MouseButton.None,
    };

    private void UpdateCursor()
    {
        Cursor = Cursors.Cross;
        if (_settings.OwnCrosshair && !_cursorHidden)
        {
            Cursor.Hide();
            _cursorHidden = true;
        }
        else if (!_settings.OwnCrosshair && _cursorHidden)
        {
            Cursor.Show();
            _cursorHidden = false;
        }
        Cursor.Clip = _settings.LockCursor && !_fullscreen
            ? new Rectangle(Bounds.Left, Bounds.Top, Bounds.Width, Bounds.Height)
            : Rectangle.Empty;
    }

    private void ToggleFullscreen() => ApplyFullscreen(!_fullscreen);

    private void ApplyFullscreen(bool value)
    {
        if (value)
        {
            if (_fullscreen) return;
            _restoreBounds = Bounds;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            _fullscreen = true;
        }
        else
        {
            if (!_fullscreen) return;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = _restoreBounds;
            _fullscreen = false;
        }
        UpdateCursor();
    }
}
