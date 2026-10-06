using System.Drawing;
using System.Numerics;
using AetherSequence.Art;
using AetherSequence.Core;

namespace AetherSequence.Entities;

/// <summary>
/// Кристалл освещения - награда за исследование комнаты.
///
/// Пока не активирован, это тёмный силуэт в центре комнаты. Чтобы его
/// разбудить, игрок подходит, целится посохом и две секунды удерживает
/// зарядку, стоя на месте. Взамен комната получает постоянный свет и
/// полностью открывается на карте, но враги внутри тоже видят игрока.
/// </summary>
internal sealed class Crystal
{
    public Vector2 Pos;

    /// <summary>Индекс комнаты, в которой стоит кристалл.</summary>
    public int RoomIndex;

    public bool Activated;

    /// <summary>Прогресс зарядки 0..1. Растёт, пока игрок держит посох.</summary>
    public float Charge;

    /// <summary>Сколько секунд игрок уже заряжает. Идёт вразнобой с анимацией.</summary>
    public float ChargeTime;

    /// <summary>Радиус постоянного света после активации.</summary>
    public const float LightRadius = 96f;

    /// <summary>Сколько секунд нужно удерживать зарядку.</summary>
    public const float ChargeSeconds = 1.5f;

    /// <summary>С какого расстояния посох вообще достаёт до кристалла.</summary>
    public const float Reach = 46f;

    /// <summary>Случайная фаза, чтобы кристаллы не пульсировали в унисон.</summary>
    public float Wave;

    public Crystal(Vector2 pos, int roomIndex, float wave)
    {
    Pos = pos;
        RoomIndex = roomIndex;
        Wave = wave;
    }

    public float ChargeRatio => Charge / ChargeSeconds;

    /// <summary>
    /// Активен ли кристалл для игрока: уже активирован либо заряжается прямо
 /// сейчас. Оба состояния дают видимый свет, иначе игрок не поймёт,
    /// куда направить посох.
 /// </summary>
    public bool GivingLight => Activated || Charge > 0.02f;

    public void Update(float dt)
    {
   if (ChargeTime > 0f)
        {
    ChargeTime = MathF.Max(0f, ChargeTime - dt);
            if (ChargeTime <= 0f) Charge = 0f;
        }
    }

    public void Reset()
    {
        Charge = 0f;
        ChargeTime = 0f;
    }

    /// <summary>
    /// Отрисовка. Неактивный кристалл - тёмный силуэт с намёком на огранку;
 /// во время зарядки наливается светом снизу вверх; после активации горит
    /// постоянно и вокруг него кружат осколки.
 /// </summary>
    public void Draw(Graphics g, float time, bool canCharge)
    {
   float bob = MathF.Sin(time * 1.6f + Wave) * 0.9f;
     float charge = ChargeRatio;
     float lit = Activated ? 1f : charge;

  float w = Sprites.SpriteCrystal.Width;
   float h = Sprites.SpriteCrystal.Height;
        float x = Pos.X - w * 0.5f;
  float y = Pos.Y - h + 5f + bob;

        // Тень под основанием - кристалл стоит на полу, а не висит.
        Palette.Disc(g, Palette.Fade(Color.Black, 0.45f), Pos.X, Pos.Y + 4f, 8f);

        // Активный кристалл светит по-настоящему и оставляет ореол.
if (Activated)
      {
            float pulse = 0.85f + 0.15f * MathF.Sin(time * 2.2f + Wave);
    Palette.AddGlow(g, Pos - new Vector2(0f, h * 0.45f), 30f * pulse, Palette.ExitOpen, 0.55f);
        }

        // Неактивный читается только силуэтом; зарядка делает его заметным.
    if (!Activated)
        {
    float hint = canCharge ? 0.22f + 0.1f * MathF.Sin(time * 5f) : 0.1f;
            Palette.AddGlow(g, Pos - new Vector2(0f, h * 0.5f), 16f, Palette.ExitOpen, hint);
        }

        if (Activated || charge > 0.5f)
  {
       Sprites.SpriteCrystal.Draw(g, x, y);
        }
    else
        {
  Sprites.SpriteCrystal.DrawSilhouette(g, x, y, GameMath.Rgb(10, 14, 30), 0.92f);
      if (charge > 0.02f)
            {
 // Проявляющийся силуэт: чем ближе к заряду, тем светлее кристалл.
        Sprites.SpriteCrystal.DrawSilhouette(g, x, y, GameMath.Mix(GameMath.Rgb(10, 14, 30), Palette.ExitOpen, charge), 0.55f);
    }
        }

        // Парящие осколки вокруг: у активного - всегда, у заряжаемого - по мере.
        float shards = Activated ? 4f : charge * 3f;
        for (int i = 0; i < (int)shards; i++)
     {
            float a = time * 0.9f + Wave + i * GameMath.Tau / 4f;
       float r = 15f + i * 2.2f;
    Vector2 p = new(Pos.X + MathF.Cos(a) * r, Pos.Y - h * 0.55f + MathF.Sin(a) * r * 0.55f);
        Color sc = GameMath.Mix(Palette.ExitOpen, Color.White, 0.3f);
        Palette.Fill(g, Palette.Fade(sc, 0.9f), p.X - 0.5f, p.Y - 1f, 2f, 3f);
     }

        // Полоса заряда: кольцо вокруг основания, растёт по часовой.
      if (!Activated && charge > 0.01f)
        {
            float ring = 10f + charge * 5f;
      float seg = charge * GameMath.Tau;
     int steps = Math.Max(1, (int)(seg * 3f));
            for (int i = 0; i < steps; i++)
      {
 float t = i / 3f;
            if (t > seg) break;
      float ang = t - MathF.PI * 0.5f;
   float rr = ring;
       float sx = Pos.X + MathF.Cos(ang) * rr;
     float sy = Pos.Y + 3f + MathF.Sin(ang) * rr * 0.5f;
        Palette.Fill(g, Palette.Fade(Palette.ExitOpen, 0.95f), sx, sy, 1.5f, 1.5f);
            }
      }
    }
}
