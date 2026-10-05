using System.Drawing;
using AetherSequence.Combat;
using AetherSequence.Core;
using AetherSequence.Entities;

namespace AetherSequence.Art;

internal enum LevelTheme
{
    Crystal,
    Toxic,
    Bone,
    Ash,
}

/// <summary>
/// Полная палитра одной темы пещеры. Ничего, что рисуется на уровне,
/// не берётся из глобального Palette - всё живёт здесь.
/// </summary>
internal sealed class ThemeColors
{
    public Color Floor;
    public Color FloorAlt;
    public Color FloorLine;

    public readonly Color[] WallBody = new Color[4];
    public Color WallTop;
    public Color WallEdge;
    public Color WallShadow;

    public Color Pillar;
    public Color PillarTop;

    public Color ExitClosed;
    public Color ExitOpen;

    /// <summary>Цвета декора: 4 вида по 3 оттенка (индекс = вид * 3 + тон).</summary>
    public readonly Color[] Decor = new Color[12];

    public Color MinimapWall;
    public Color MinimapFloor;
    public Color MinimapPillar;
    public Color Vignette;

    // --- Тёмный sci-fi + неон -------------------------------------------------------
    // Слои читаются по яркости: дальний фон самый тёмный, стена светлее пола,
    // неоновая кромка стены и объекты - самые яркие. Разница между слоями
    // раньше была почти нулевой, из-за чего карта читалась как плоская.

    /// <summary>Самый дальний слой: фон за стенами и пустота.</summary>
    public Color Deep;

    /// <summary>Разделительный шов между панелями пола.</summary>
    public Color Seam;

    /// <summary>Светлая верхняя грань панели пола - ловит свет сверху.</summary>
    public Color PanelLit;

    /// <summary>Лицевая (нижняя) сторона блока стены - уходит в тень.</summary>
    public Color WallFace;

    /// <summary>Неоновая кромка верхней грани стены.</summary>
    public Color Neon;

    /// <summary>Мягкий ореол неона вокруг кромки.</summary>
    public Color NeonGlow;

    /// <summary>Цвет проводов и вентиляции на панелях.</summary>
    public Color Cable;

    /// <summary>Цвет редких деталей (антенны, индикаторы).</summary>
    public Color Indicator;

    public string Name = string.Empty;
    public string AmbientHint = string.Empty;
}

/// <summary>
/// Разбивка глубин по темам. Тема - чистая функция от глубины:
/// никакого RNG, поэтому генерация уровня остаётся детерминированной
/// и тесты не ломаются. По 4 глубины на тему.
/// </summary>
internal static class LevelThemes
{
    public const int DepthsPerTheme = 4;
    public const int Count = 4;

    public static LevelTheme ForDepth(int depth) => (LevelTheme)((Math.Max(1, depth) - 1) / DepthsPerTheme % Count);

    public static int FirstDepth(LevelTheme theme) => (int)theme * DepthsPerTheme + 1;

    public static int LastDepth(LevelTheme theme) => (int)theme * DepthsPerTheme + DepthsPerTheme;

    private static readonly ThemeColors[] Palette =
    {
        // 0 — КРИСТАЛЬНАЯ ГРОТА: холодный сине-фиолетовый камень, cyan-неон
        Build(
            "КРИСТАЛЬНАЯ ГРОТА",
floor: (36, 37, 62), floorAlt: (43, 44, 73), floorLine: (24, 25, 42),
wall: (32, 33, 58), wallTop: (72, 76, 124), wallEdge: (110, 120, 180), wallShadow: (10, 10, 20),
            pillar: (36, 38, 68), pillarTop: (74, 80, 130),
            exitClosed: (34, 44, 74), exitOpen: (150, 220, 255),
            decor: new[]
            {
                (38, 62, 74), (52, 84, 96), (28, 46, 58),   // мох (холодный)
                (14, 15, 26), (14, 15, 26), (14, 15, 26),   // трещины
                (40, 42, 62), (58, 60, 84), (40, 42, 62),   // щебень
                (40, 74, 100), (86, 150, 196), (40, 74, 100), // кристалл
            },
            minimapWall: (7, 7, 14), minimapFloor: (52, 54, 88), minimapPillar: (104, 110, 170),
            vignette: (5, 5, 12),
            neon: (86, 196, 246), deep: (10, 11, 22)),

        // 1 — ЯДОВИТЫЕ ОЗЁРА: болотный зелёно-жёлтый, зелёный неон
        Build(
            "ЯДОВИТЫЕ ОЗЁРА",
            floor: (33, 42, 34), floorAlt: (40, 51, 40), floorLine: (22, 30, 24),
      wall: (30, 38, 31), wallTop: (84, 112, 68), wallEdge: (124, 152, 88), wallShadow: (12, 17, 13),
            pillar: (33, 46, 33), pillarTop: (62, 84, 52),
            exitClosed: (44, 62, 30), exitOpen: (196, 255, 128),
            decor: new[]
            {
                (48, 74, 38), (66, 96, 48), (36, 58, 32),   // грибы
                (13, 19, 14), (13, 19, 14), (13, 19, 14),   // трещины
                (40, 50, 36), (58, 70, 48), (40, 50, 36),   // ил
                (58, 92, 34), (112, 156, 52), (58, 92, 34),  // слизь
            },
            minimapWall: (6, 10, 7), minimapFloor: (54, 68, 48), minimapPillar: (108, 132, 74),
            vignette: (6, 11, 7),
            neon: (126, 224, 96), deep: (9, 14, 10)),

        // 2 — КОСТЯНАЯ КАТАКОМБА: серо-костяной, сухой, тёплый неон
        Build(
            "КОСТЯНАЯ КАТАКОМБА",
floor: (43, 40, 36), floorAlt: (52, 49, 44), floorLine: (30, 28, 25),
       wall: (38, 36, 32), wallTop: (100, 94, 82), wallEdge: (142, 134, 118), wallShadow: (15, 14, 12),
            pillar: (41, 39, 36), pillarTop: (70, 66, 60),
            exitClosed: (56, 52, 44), exitOpen: (244, 234, 204),
            decor: new[]
            {
                (98, 92, 78), (120, 114, 98), (78, 74, 62),  // кости
                (17, 16, 15), (17, 16, 15), (17, 16, 15),    // трещины
                (48, 46, 42), (64, 61, 56), (48, 46, 42),    // гравий
                (122, 117, 100), (152, 146, 128), (122, 117, 100), // череп
            },
            minimapWall: (8, 8, 7), minimapFloor: (68, 65, 60), minimapPillar: (118, 113, 102),
            vignette: (10, 9, 8),
            neon: (238, 214, 150), deep: (12, 11, 10)),

        // 3 — ПЕПЕЛЬНЫЕ ПУСТОШИ: багрово-чёрный, жар, оранжевый неон
        Build(
            "ПЕПЕЛЬНЫЕ ПУСТОШИ",
            floor: (44, 32, 31), floorAlt: (54, 39, 36), floorLine: (30, 22, 21),
    wall: (34, 25, 24), wallTop: (104, 62, 46), wallEdge: (156, 94, 62), wallShadow: (14, 9, 9),
            pillar: (40, 28, 26), pillarTop: (78, 48, 36),
            exitClosed: (62, 34, 26), exitOpen: (255, 172, 92),
            decor: new[]
            {
                (68, 42, 26), (94, 58, 34), (48, 30, 20),   // угли
                (15, 10, 9), (15, 10, 9), (15, 10, 9),     // трещины
                (44, 33, 31), (60, 44, 40), (44, 33, 31),   // пепел
                (122, 54, 26), (176, 102, 40), (122, 54, 26), // лава
            },
            minimapWall: (9, 6, 6), minimapFloor: (64, 42, 38), minimapPillar: (128, 76, 54),
            vignette: (12, 7, 6),
            neon: (255, 138, 62), deep: (13, 8, 8)),
    };

    public static ThemeColors Of(LevelTheme theme) => Palette[(int)theme % Palette.Length];

    public static ThemeColors Of(int depth) => Of(ForDepth(depth));

    public static string NameOf(int depth) => Of(depth).Name;

    /// <summary>Пул врагов темы: каждый элемент добавляется с весом 1.</summary>
    public static List<EnemyKind> EnemyPool(int depth) => ForDepth(depth) switch
    {
        LevelTheme.Crystal => [EnemyKind.Slime, EnemyKind.Slime, EnemyKind.MiniSlime, EnemyKind.Knight],
        LevelTheme.Toxic => [EnemyKind.Slime, EnemyKind.Slime, EnemyKind.MiniSlime, EnemyKind.Shade],
        LevelTheme.Bone => [EnemyKind.Knight, EnemyKind.Shade, EnemyKind.Sentinel],
        _ => [EnemyKind.Sentinel, EnemyKind.Shade, EnemyKind.Knight, EnemyKind.Shade],
    };

    private static ThemeColors Build(
        string name,
        (int R, int G, int B) floor,
        (int R, int G, int B) floorAlt,
        (int R, int G, int B) floorLine,
        (int R, int G, int B) wall,
        (int R, int G, int B) wallTop,
        (int R, int G, int B) wallEdge,
        (int R, int G, int B) wallShadow,
        (int R, int G, int B) pillar,
        (int R, int G, int B) pillarTop,
        (int R, int G, int B) exitClosed,
        (int R, int G, int B) exitOpen,
        (int R, int G, int B)[] decor,
        (int R, int G, int B) minimapWall,
        (int R, int G, int B) minimapFloor,
        (int R, int G, int B) minimapPillar,
        (int R, int G, int B) vignette,
        (int R, int G, int B) neon,
        (int R, int G, int B) deep)
    {
        ThemeColors t = new()
        {
            Name = name,
            Floor = GameMath.Rgb(floor.R, floor.G, floor.B),
            FloorAlt = GameMath.Rgb(floorAlt.R, floorAlt.G, floorAlt.B),
            FloorLine = GameMath.Rgb(floorLine.R, floorLine.G, floorLine.B),
            WallTop = GameMath.Rgb(wallTop.R, wallTop.G, wallTop.B),
            WallEdge = GameMath.Rgb(wallEdge.R, wallEdge.G, wallEdge.B),
            WallShadow = GameMath.Rgb(wallShadow.R, wallShadow.G, wallShadow.B),
            Pillar = GameMath.Rgb(pillar.R, pillar.G, pillar.B),
            PillarTop = GameMath.Rgb(pillarTop.R, pillarTop.G, pillarTop.B),
            ExitClosed = GameMath.Rgb(exitClosed.R, exitClosed.G, exitClosed.B),
            ExitOpen = GameMath.Rgb(exitOpen.R, exitOpen.G, exitOpen.B),
            MinimapWall = GameMath.Rgb(minimapWall.R, minimapWall.G, minimapWall.B),
            MinimapFloor = GameMath.Rgb(minimapFloor.R, minimapFloor.G, minimapFloor.B),
            MinimapPillar = GameMath.Rgb(minimapPillar.R, minimapPillar.G, minimapPillar.B),
            Vignette = GameMath.Rgb(vignette.R, vignette.G, vignette.B),
        };

        // Тёмный sci-fi: пол и стены притемняем, неон задаёт акцент.
        // Панель пола светлее фона, но всё ещё намного темнее неона.
        t.Deep = GameMath.Rgb(deep.R, deep.G, deep.B);
        t.Neon = GameMath.Rgb(neon.R, neon.G, neon.B);
        t.NeonGlow = GameMath.Shade(t.Neon, 1f);
        t.Seam = GameMath.Shade(t.FloorLine, 0.7f);
        t.PanelLit = GameMath.Mix(t.FloorAlt, t.Neon, 0.10f);
        t.WallFace = GameMath.Shade(t.WallTop, 0.34f);
        t.Cable = GameMath.Mix(t.FloorLine, t.Neon, 0.22f);
        t.Indicator = GameMath.Mix(t.Neon, Color.White, 0.35f);

        // Четыре слегка разных тона стены - дают кладку без шума на весь экран.
        // Стена должна быть заметно светлее пола: это и делает её возвышенностью.
        Color baseWall = GameMath.Rgb(wall.R, wall.G, wall.B);
        t.WallBody[0] = baseWall;
        t.WallBody[1] = GameMath.Shade(baseWall, 1.10f);
        t.WallBody[2] = GameMath.Shade(baseWall, 0.90f);
        t.WallBody[3] = GameMath.Shade(baseWall, 1.18f);

        for (int i = 0; i < 12 && i < decor.Length; i++)
        {
            t.Decor[i] = GameMath.Rgb(decor[i].R, decor[i].G, decor[i].B);
        }

        return t;
    }
}
