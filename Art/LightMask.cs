using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using AetherSequence.Core;
using AetherSequence.World;

namespace AetherSequence.Art;

/// <summary>Один источник света: круг либо конус.</summary>
internal readonly struct LightSource
{
    public readonly Vector2 Pos;
    public readonly float Radius;
    public readonly Color Color;
    public readonly float Intensity;
    public readonly Vector2 Dir;
    public readonly float HalfAngle;
    public readonly float Softness;
    public readonly bool HasCone;

    /// <summary>Свет не должен проходить сквозь стены (посох, кристаллы).</summary>
    public readonly bool Occluded;

    private LightSource(Vector2 pos, float radius, Color color, float intensity,
      Vector2 dir, float halfAngle, float softness, bool hasCone, bool occluded)
    {
        Pos = pos;
        Radius = radius;
        Color = color;
    Intensity = intensity;
        Dir = dir;
 HalfAngle = halfAngle;
   Softness = softness;
        HasCone = hasCone;
      Occluded = occluded;
    }

    /// <summary>Круглый источник: ореол вокруг точки.</summary>
    public static LightSource Circle(Vector2 pos, float radius, Color color, float intensity,
        float softness = 0.75f, bool occluded = false)
        => new(pos, radius, color, intensity, Vector2.UnitX, 0f, softness, false, occluded);

    /// <summary>Конус: свет только в направлении dir - так работает посох.</summary>
    public static LightSource Cone(Vector2 pos, Vector2 dir, float radius, float halfAngle,
    Color color, float intensity, float softness = 0.6f)
    => new(pos, radius, color, intensity, GameMath.Normalized(dir), halfAngle, softness, true, true);

    /// <summary>
    /// Прямоугольник маски, затрагиваемый источником. Обход идёт только
    /// по нему - ради этого маска и считается в низком разрешении.
    /// </summary>
    public void Bounds(Vector2 cam, out int x0, out int y0, out int x1, out int y1)
    {
float s = 1f / LightMask.ScaleDiv;
   x0 = (int)MathF.Floor((Pos.X - Radius - cam.X) * s) - 1;
    y0 = (int)MathF.Floor((Pos.Y - Radius - cam.Y) * s) - 1;
        x1 = (int)MathF.Ceiling((Pos.X + Radius - cam.X) * s) + 1;
        y1 = (int)MathF.Ceiling((Pos.Y + Radius - cam.Y) * s) + 1;
    }
}

/// <summary>
/// Счётчики времени маски. Нужны, потому что темнота - единственная часть
/// рендера, которая считается целиком каждый кадр: если она дорожает, 60 FPS
/// становится недостижимым, и это видно только по замеру.
/// </summary>
internal static class LightMaskTimings
{
    public static double LastAccumulateMs;
    public static double LastPaintMs;
    public static double LastApplyMs;

  public static void Reset()
    {
        LastAccumulateMs = 0;
        LastPaintMs = 0;
 LastApplyMs = 0;
    }
}

/// <summary>
/// Маска освещённости в низком разрешении.
///
/// Мир рисуется как обычно, затем накрывается маской темноты. Считать её в
/// разрешении кадра дорого, поэтому она живёт в 160x90 и растягивается при
/// выводе - в 16 раз меньше работы на пиксель.
///
/// Скорость держится на том, что для каждого источника считается только
/// прямоугольник его радиуса: пятно радиуса 40 px - это около 400 пикселей
/// маски, а не весь кадр.
/// </summary>
internal sealed class LightMask
{
    public const int ScaleDiv = 4;

private readonly int _w;
    private readonly int _h;
    private readonly Bitmap _bmp;
    private readonly float[] _levels;
    private readonly Color[] _tint;
    private readonly bool[] _tinted;
    private readonly List<LightSource> _pending = new();

    private Level? _level;

    /// <summary>Глубина темноты вне света: 0 - светло, 1 - абсолютная тьма.</summary>
    public float Darkness { get; set; } = 0.9f;

    /// <summary>Цвет темноты - им заливается неосвещённое.</summary>
    private Color _fog = Color.FromArgb(6, 7, 16);

    public LightMask(int frameW, int frameH)
    {
        _w = Math.Max(1, frameW / ScaleDiv);
        _h = Math.Max(1, frameH / ScaleDiv);
        _bmp = new Bitmap(_w, _h, PixelFormat.Format32bppArgb);
        _levels = new float[_w * _h];
        _tint = new Color[_w * _h];
        _tinted = new bool[_w * _h];
    }

    public int Width => _w;

    public int Height => _h;

    /// <summary>Уровень нужен для проверки "проходит ли свет сквозь стену".</summary>
    public void Bind(Level level, Color fog)
    {
        _level = level;
        _fog = fog;
    }

    /// <summary>Очистить список источников перед новым кадром.</summary>
    public void Begin() => _pending.Clear();

    /// <summary>Добавить источник в текущий кадр.</summary>
    public void Add(LightSource src) => _pending.Add(src);

    /// <summary>Количество источников, заявленных в этом кадре.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Пересчитать маску по накопленным источникам. Вызывается один раз
    /// в кадре, перед отрисовкой мира.
    /// </summary>
public void Build(Vector2 cam, float intensityScale = 1f)
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        float floor = 1f - Darkness;
        for (int i = 0; i < _levels.Length; i++)
        {
      _levels[i] = floor;
            _tinted[i] = false;
            _tint[i] = _fog;
        }

        for (int i = 0; i < _pending.Count; i++)
        {
            Accumulate(_pending[i], cam, intensityScale);
        }

LightMaskTimings.LastAccumulateMs = sw.Elapsed.TotalMilliseconds;
        sw.Restart();

   // Маска мелкая (в четыре раза меньше кадра), поэтому растяжка
   // превращала края света в лестницу ступенек. Размываем на два прохода
 // по строкам и столбцам: separable blur стоит копейки, а свет выходит
  // мягким. Маска при этом остаётся дешёвой.
        Blur();

        Paint();
        LightMaskTimings.LastPaintMs = sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>Размыв маски по гауссу 1-2-1 в две полосы.</summary>
    private void Blur()
    {
        const float W = 0.25f;
     const float C = 0.5f;

        // Горизонталь: центр берёт полвеса, соседи по четверти.
    for (int y = 0; y < _h; y++)
        {
            int row = y * _w;
            float prev = _levels[row];
      for (int x = 0; x < _w; x++)
  {
      float cur = _levels[row + x];
     float next = x + 1 < _w ? _levels[row + x + 1] : cur;
       _levels[row + x] = prev * W + cur * C + next * W;
   prev = cur;
      }
        }

        // Вертикаль: то же по столбцам.
        for (int x = 0; x < _w; x++)
        {
     float prev = _levels[x];
     for (int y = 0; y < _h; y++)
   {
     int i = y * _w + x;
     float cur = _levels[i];
          float next = y + 1 < _h ? _levels[i + _w] : cur;
  _levels[i] = prev * W + cur * C + next * W;
    prev = cur;
   }
        }
    }

    private void Accumulate(LightSource src, Vector2 cam, float intensityScale)
    {
        src.Bounds(cam, out int x0, out int y0, out int x1, out int y1);
   if (x1 < 0 || y1 < 0 || x0 >= _w || y0 >= _h) return;

   if (x0 < 0) x0 = 0;
        if (y0 < 0) y0 = 0;
        if (x1 >= _w) x1 = _w - 1;
   if (y1 >= _h) y1 = _h - 1;

        float radius = src.Radius;
        if (radius <= 0.5f) return;

    float invR = 1f / radius;
        float radiusSq = radius * radius;
        float cosLimit = src.HasCone ? MathF.Cos(GameMath.Clamp(src.HalfAngle, 0.08f, 1.5f)) : -2f;
    float hard = 1f - GameMath.Clamp(src.Softness, 0.05f, 1f);

     for (int my = y0; my <= y1; my++)
    {
            float wy = cam.Y + (my + 0.5f) * ScaleDiv - src.Pos.Y;
            int row = my * _w;

            for (int mx = x0; mx <= x1; mx++)
   {
 float wx = cam.X + (mx + 0.5f) * ScaleDiv - src.Pos.X;
      float dSq = wx * wx + wy * wy;
       if (dSq > radiusSq) continue;

   float dist = MathF.Sqrt(dSq) * invR;

       if (src.HasCone)
    {
     float dot = dist > 0.02f ? (wx * src.Dir.X + wy * src.Dir.Y) / (dist * radius) : 1f;
     if (dot < cosLimit) continue;
      }

            // Свет не должен просачиваться сквозь стены: если между
        // источником и точкой стена, пиксель остаётся тёмным.
      if (src.Occluded && _level is not null && dSq > 16f)
            {
     if (_level.SolidAt(new Vector2(src.Pos.X + wx * 0.5f, src.Pos.Y + wy * 0.5f))) continue;
   }

        float t = 1f - dist;
      float fall = t * hard + t * t * (1f - hard);

   float add = src.Intensity * fall * intensityScale;
if (add <= 0.003f) continue;

         int idx = row + mx;
         _levels[idx] += add;
      if (add > 0.02f)
                {
       _tinted[idx] = true;
         _tint[idx] = src.Color;
      }
         }
        }
    }

/// <summary>
    /// Раскладывает посчитанные уровни в bitmap. Пиксель несёт альфу
    /// темноты: чем больше света, тем прозрачнее, чтобы мир проступал.
    /// </summary>
    private void Paint()
    {
        BitmapData data = _bmp.LockBits(new Rectangle(0, 0, _w, _h),
      ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
       unsafe
            {
          for (int y = 0; y < _h; y++)
      {
      byte* row = (byte*)data.Scan0 + (long)y * data.Stride;
       int src = y * _w;

    for (int x = 0; x < _w; x++)
         {
 int i = src + x;
      float v = _levels[i];
      if (v > 1f) v = 1f;
       else if (v < 0f) v = 0f;

         // Прозрачность: в темноте слой плотный, в свете его нет вовсе.
         // Поэтому освещённый мир проступает без потери цвета.
   int alpha = (int)((1f - v) * 255f);
            if (alpha < 0) alpha = 0;
        else if (alpha > 255) alpha = 255;

   Color c = _tinted[i]
        ? GameMath.Mix(_fog, _tint[i], v)
          : _fog;

        byte* px = row + x * 4;
      px[0] = c.B;
     px[1] = c.G;
     px[2] = c.R;
        px[3] = (byte)alpha;
          }
            }
            }
        }
        finally
    {
        _bmp.UnlockBits(data);
   }
    }

/// <summary>
    /// Накрывает уже нарисованный мир маской темноты. CompositingMode.Multiply
    /// в System.Drawing.Common недоступен, поэтому темнота идёт обычным
    /// SourceOver: маска несёт альфу, и в освещённых местах её просто нет,
    /// так что мир проступает без изменения. Там, где свет есть, цвет
    /// подмешивается - это и даёт окрашивание света.
    /// </summary>
public void Apply(Graphics target, int w, int h)
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        CompositingMode previous = target.CompositingMode;
        InterpolationMode prevInterp = target.InterpolationMode;
        PixelOffsetMode prevOffset = target.PixelOffsetMode;

target.CompositingMode = CompositingMode.SourceOver;

        // NearestNeighbor, а не Bilinear: билинейная растяжка маски с
 // альфой на 640x360 стоит 14 мс - почти целый кадр. Ближайший сосед
     // в разы дешевле, а для пиксельной игры блочные края света даже
  // уместнее: свет ложится теми же квадратами, что и тайлы.
        target.InterpolationMode = InterpolationMode.NearestNeighbor;
        target.PixelOffsetMode = PixelOffsetMode.Half;
        target.DrawImage(_bmp, new RectangleF(0f, 0f, w, h), new Rectangle(0, 0, _w, _h), GraphicsUnit.Pixel);

        target.CompositingMode = previous;
    target.InterpolationMode = prevInterp;
        target.PixelOffsetMode = prevOffset;

  LightMaskTimings.LastApplyMs = sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// Освещённость мировой точки: 0 - тьма, 1 - полный свет. Этим
    /// пользуется ИИ, чтобы решить, атаковать или отступить в темноту.
    /// </summary>
    public float LevelAt(Vector2 world, Vector2 cam)
    {
     int mx = (int)MathF.Floor((world.X - cam.X) / ScaleDiv);
        int my = (int)MathF.Floor((world.Y - cam.Y) / ScaleDiv);
        if (mx < 0 || my < 0 || mx >= _w || my >= _h) return 0f;
   return MathF.Min(1f, _levels[my * _w + mx]);
    }

    /// <summary>Освещена ли точка настолько, чтобы её было видно.</summary>
    public bool IsLit(Vector2 world, Vector2 cam, float threshold = 0.3f)
        => LevelAt(world, cam) >= threshold;

    /// <summary>
    /// Копия маски для тестов и отладки: позволяет посмотреть темноту
 /// без запуска окна. Обычному коду не нужна.
    /// </summary>
    public Bitmap Snapshot() => new(_bmp);

    public void Dispose() => _bmp.Dispose();
}
