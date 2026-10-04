using System.Drawing;
using AetherSequence.Core;

namespace AetherSequence.Combat;

internal enum SpellKind
{
    Bolt,
    Nova,
    Cone,
    Field,
    Rain,
    Beam,
}

internal sealed class SpellDef
{
    public string Combo = string.Empty;
    public string Name = string.Empty;
    public Element Element;
    public SpellKind Kind;
    public float Damage;
    public int Cost;
    public int Count = 1;
    public float Spread;
    public float Speed = 150f;
    public float Radius;
    public float Angle = GameMath.Tau;
    public float Knockback;
    public float Stun;
    public float Pull;
    public float Slow;
    public float Duration = 2.2f;
    public float Delay;
    public float Life = 2.4f;
    public float BeamLength = 220f;
    public float BeamWidth = 14f;
    public int Pierce;
    public bool Homing;
    public bool Explode;
    public float ExplodeRadius = 28f;
    public float Heal;
    public float SelfDamage;
    public float Shake = 3f;
    public float FlowGain = 0.13f;
    public Color Color = Color.White;
    public bool Fusion;
    public int Score = 1;

    /// <summary>Растровая анимация каста. Пусто - подберётся по стихии и виду.</summary>
    public string Sheet = string.Empty;

    /// <summary>Лист, заданный явно, либо подобранный по стихии и виду заклинания.</summary>
    public string SheetKey => string.IsNullOrEmpty(Sheet) ? SpellDB.SheetFor(Element, Kind) : Sheet;
}

internal static class SpellDB
{
    public static readonly List<SpellDef> All = new();

    public static readonly Dictionary<string, SpellDef> ByCombo = new(StringComparer.Ordinal);

    public static readonly Dictionary<(Element, Element), SpellDef> Fusions = new();

    private static Color C(Element e) => Elements.Color(e);

    static SpellDB()
    {
        Add(new SpellDef
        {
            Combo = "F", Name = "Искра огня", Element = Element.Fire, Kind = SpellKind.Bolt,
            Damage = 5f, Cost = 4, Speed = 155f, Life = 1.3f, Color = C(Element.Fire),
        });
        Add(new SpellDef
        {
            Combo = "W", Name = "Порыв ветра", Element = Element.Wind, Kind = SpellKind.Cone,
            Damage = 3f, Cost = 4, Radius = 28f, Angle = 1.7f, Knockback = 140f, Color = C(Element.Wind), FlowGain = 0.1f,
        });
        Add(new SpellDef
        {
            Combo = "A", Name = "Капля", Element = Element.Water, Kind = SpellKind.Bolt,
            Damage = 4f, Cost = 4, Speed = 135f, Life = 1.2f, Color = C(Element.Water),
        });
        Add(new SpellDef
        {
            Combo = "E", Name = "Камешек", Element = Element.Earth, Kind = SpellKind.Bolt,
            Damage = 7f, Cost = 5, Speed = 95f, Life = 1.5f, Knockback = 130f, Color = C(Element.Earth),
        });
        Add(new SpellDef
        {
            Combo = "D", Name = "Осколок тьмы", Element = Element.Dark, Kind = SpellKind.Bolt,
            Damage = 6f, Cost = 5, Speed = 120f, Life = 1.8f, Homing = true, Color = C(Element.Dark),
        });
        Add(new SpellDef
        {
            Combo = "L", Name = "Искра света", Element = Element.Light, Kind = SpellKind.Bolt,
            Damage = 5f, Cost = 4, Speed = 175f, Life = 1.2f, Pierce = 1, Color = C(Element.Light),
        });

        Add(new SpellDef
        {
            Combo = "FF", Name = "Огненный снаряд", Element = Element.Fire, Kind = SpellKind.Bolt,
            Damage = 11f, Cost = 9, Speed = 195f, Life = 1.6f, Explode = true, ExplodeRadius = 20f, Color = C(Element.Fire), Score = 2,
        });
        Add(new SpellDef
        {
            Combo = "FW", Name = "Огненный порыв", Element = Element.Fire, Kind = SpellKind.Cone,
            Damage = 7f, Cost = 10, Radius = 36f, Angle = 1.9f, Knockback = 160f, Color = GameMath.Mix(C(Element.Fire), C(Element.Wind), 0.35f), Score = 2,
        });
        Add(new SpellDef
        {
            Combo = "FA", Name = "Плазменный плевок", Element = Element.Fire, Kind = SpellKind.Bolt,
            Damage = 9f, Cost = 9, Speed = 150f, Life = 1.5f, Slow = 0.35f, Color = GameMath.Mix(C(Element.Fire), C(Element.Water), 0.4f), Score = 2,
        });
        Add(new SpellDef
        {
            Combo = "WW", Name = "Ветровой клинок", Element = Element.Wind, Kind = SpellKind.Bolt,
            Damage = 10f, Cost = 9, Speed = 265f, Life = 1.5f, Pierce = 3, Color = C(Element.Wind), Score = 2,
        });
        Add(new SpellDef
        {
            Combo = "WA", Name = "Ледяная игла", Element = Element.Water, Kind = SpellKind.Bolt,
            Damage = 9f, Cost = 10, Speed = 205f, Life = 1.6f, Pierce = 2, Slow = 0.5f, Color = GameMath.Mix(C(Element.Water), Color.White, 0.3f), Score = 2,
        });
        Add(new SpellDef
        {
            Combo = "WE", Name = "Песчаная струя", Element = Element.Wind, Kind = SpellKind.Cone,
            Damage = 8f, Cost = 10, Radius = 38f, Angle = 1.2f, Knockback = 100f, Color = GameMath.Mix(C(Element.Wind), C(Element.Earth), 0.5f), Score = 2,
        });
        Add(new SpellDef
        {
            Combo = "AA", Name = "Водяной шар", Element = Element.Water, Kind = SpellKind.Bolt,
            Damage = 10f, Cost = 10, Speed = 140f, Life = 1.6f, Explode = true, ExplodeRadius = 24f, Slow = 0.4f, Color = C(Element.Water), Score = 2,
        });
        Add(new SpellDef
        {
            Combo = "AE", Name = "Грязевой снаряд", Element = Element.Earth, Kind = SpellKind.Bolt,
            Damage = 10f, Cost = 10, Speed = 110f, Life = 1.6f, Slow = 0.55f, Color = GameMath.Mix(C(Element.Earth), C(Element.Water), 0.5f), Score = 2,
        });
        Add(new SpellDef
        {
            Combo = "EE", Name = "Каменный молот", Element = Element.Earth, Kind = SpellKind.Bolt,
            Damage = 14f, Cost = 11, Speed = 90f, Life = 1.8f, Knockback = 210f, Explode = true, ExplodeRadius = 18f, Color = C(Element.Earth), Score = 2,
        });
        Add(new SpellDef
        {
            Combo = "DD", Name = "Тёмный клинок", Element = Element.Dark, Kind = SpellKind.Bolt,
            Damage = 12f, Cost = 12, Speed = 165f, Life = 2.2f, Pierce = 1, Homing = true, Color = C(Element.Dark), Score = 2,
        });
        Add(new SpellDef
        {
            Combo = "LL", Name = "Световая стрела", Element = Element.Light, Kind = SpellKind.Bolt,
            Damage = 12f, Cost = 12, Speed = 245f, Life = 1.6f, Pierce = 3, Color = C(Element.Light), Score = 2,
        });

        Add(new SpellDef
        {
            Combo = "FFF", Name = "Огненный шар", Element = Element.Fire, Kind = SpellKind.Bolt,
            Damage = 20f, Cost = 18, Speed = 155f, Life = 2.2f, Explode = true, ExplodeRadius = 34f, Color = C(Element.Fire), Score = 3, Shake = 5f,
        });
        Add(new SpellDef
        {
            Combo = "FWW", Name = "Огненный вихрь", Element = Element.Fire, Kind = SpellKind.Field,
            Damage = 22f, Cost = 24, Radius = 36f, Duration = 2.6f, Pull = 30f, Color = GameMath.Mix(C(Element.Fire), C(Element.Wind), 0.45f), Score = 3,
        });
        Add(new SpellDef
        {
            Combo = "WWW", Name = "Циклон", Element = Element.Wind, Kind = SpellKind.Nova,
            Damage = 15f, Cost = 20, Radius = 64f, Knockback = 230f, Color = C(Element.Wind), Score = 3, Shake = 4f,
        });
        Add(new SpellDef
        {
            Combo = "WAA", Name = "Снежный вихрь", Element = Element.Water, Kind = SpellKind.Field,
            Damage = 16f, Cost = 22, Radius = 38f, Duration = 3f, Slow = 0.5f, Color = GameMath.Mix(C(Element.Water), Color.White, 0.35f), Score = 3,
        });
        Add(new SpellDef
        {
            Combo = "AAA", Name = "Гидрошок", Element = Element.Water, Kind = SpellKind.Nova,
            Damage = 16f, Cost = 22, Radius = 58f, Stun = 0.7f, Color = C(Element.Water), Score = 3, Shake = 4f,
        });
        Add(new SpellDef
        {
            Combo = "AAE", Name = "Цунакремень", Element = Element.Water, Kind = SpellKind.Cone,
            Damage = 15f, Cost = 21, Radius = 70f, Angle = 1.4f, Slow = 0.6f, Color = GameMath.Mix(C(Element.Water), C(Element.Earth), 0.5f), Score = 3,
        });
        Add(new SpellDef
        {
            Combo = "EEE", Name = "Землетрясение", Element = Element.Earth, Kind = SpellKind.Nova,
            Damage = 19f, Cost = 24, Radius = 76f, Stun = 1.1f, Color = C(Element.Earth), Score = 3, Shake = 7f,
        });
        Add(new SpellDef
        {
            Combo = "DDD", Name = "Чёрная дыра", Element = Element.Dark, Kind = SpellKind.Field,
            Damage = 20f, Cost = 26, Radius = 42f, Duration = 3.2f, Pull = 160f, Color = C(Element.Dark), Score = 3, Shake = 5f,
        });
        Add(new SpellDef
        {
            Combo = "LLL", Name = "Солнечная вспышка", Element = Element.Light, Kind = SpellKind.Nova,
            Damage = 22f, Cost = 24, Radius = 74f, Stun = 0.9f, Color = C(Element.Light), Score = 3, Shake = 6f,
        });
        Add(new SpellDef
        {
            Combo = "DAL", Name = "Затмение", Element = Element.Dark, Kind = SpellKind.Beam,
            Damage = 26f, Cost = 26, BeamLength = 240f, BeamWidth = 16f, Color = GameMath.Mix(C(Element.Dark), C(Element.Light), 0.5f), Score = 4, Shake = 6f,
        });
        Add(new SpellDef
        {
            Combo = "FAE", Name = "Магма", Element = Element.Fire, Kind = SpellKind.Nova,
            Damage = 23f, Cost = 23, Radius = 66f, Color = GameMath.Mix(C(Element.Fire), C(Element.Earth), 0.5f), Score = 3, Shake = 6f,
        });
        Add(new SpellDef
        {
            Combo = "FAD", Name = "Адское пламя", Element = Element.Dark, Kind = SpellKind.Bolt,
            Damage = 18f, Cost = 24, Count = 3, Spread = 0.5f, Speed = 175f, Life = 2f, Explode = true, ExplodeRadius = 30f,
            Color = GameMath.Mix(C(Element.Fire), C(Element.Dark), 0.55f), Score = 4, Shake = 5f,
        });
        Add(new SpellDef
        {
            Combo = "DDE", Name = "Провал", Element = Element.Dark, Kind = SpellKind.Field,
            Damage = 18f, Cost = 23, Radius = 46f, Duration = 3f, Pull = 120f, Color = GameMath.Mix(C(Element.Dark), C(Element.Earth), 0.5f), Score = 3,
        });

        Add(new SpellDef
        {
            Combo = "FFFF", Name = "Метеоритный дождь", Element = Element.Fire, Kind = SpellKind.Rain,
            Damage = 26f, Cost = 34, Count = 7, Radius = 26f, Delay = 0.12f, Color = C(Element.Fire), Score = 5, Shake = 8f,
        });
        Add(new SpellDef
        {
            Combo = "WWWW", Name = "Торнадо", Element = Element.Wind, Kind = SpellKind.Field,
            Damage = 28f, Cost = 34, Radius = 42f, Duration = 4f, Pull = 60f, Knockback = 200f, Color = C(Element.Wind), Score = 5, Shake = 6f,
        });
        Add(new SpellDef
        {
            Combo = "AAAA", Name = "Прилив", Element = Element.Water, Kind = SpellKind.Nova,
            Damage = 26f, Cost = 32, Radius = 106f, Knockback = 290f, Color = C(Element.Water), Score = 5, Shake = 7f,
        });
        Add(new SpellDef
        {
            Combo = "EEEE", Name = "Тектонический удар", Element = Element.Earth, Kind = SpellKind.Nova,
            Damage = 30f, Cost = 36, Radius = 100f, Stun = 1.6f, Color = C(Element.Earth), Score = 5, Shake = 11f,
        });
        Add(new SpellDef
        {
            Combo = "DDDD", Name = "Пустота", Element = Element.Dark, Kind = SpellKind.Nova,
            Damage = 30f, Cost = 36, Radius = 92f, Pull = 170f, Heal = 8f, Color = C(Element.Dark), Score = 5, Shake = 8f,
        });
        Add(new SpellDef
        {
            Combo = "LLLL", Name = "Суждение", Element = Element.Light, Kind = SpellKind.Rain,
            Damage = 26f, Cost = 36, Count = 9, Radius = 24f, Delay = 0.08f, Color = C(Element.Light), Score = 5, Shake = 9f,
        });
        Add(new SpellDef
        {
            Combo = "FFWW", Name = "Плазменный вихрь", Element = Element.Fire, Kind = SpellKind.Field,
            Damage = 30f, Cost = 32, Radius = 46f, Duration = 3.4f, Pull = 70f, Color = GameMath.Mix(C(Element.Fire), C(Element.Wind), 0.5f), Score = 5, Shake = 6f,
        });
        Add(new SpellDef
        {
            Combo = "AALL", Name = "Ледяная кара", Element = Element.Water, Kind = SpellKind.Beam,
            Damage = 30f, Cost = 34, BeamLength = 260f, BeamWidth = 20f, Stun = 0.8f, Color = GameMath.Mix(C(Element.Water), C(Element.Light), 0.5f), Score = 5, Shake = 6f,
        });

        Fusion(Element.Fire, Element.Wind, "Огненное торнадо", new SpellDef
        {
            Kind = SpellKind.Field, Damage = 30f, Cost = 28, Radius = 54f, Duration = 3.6f, Pull = 55f, Color = GameMath.Mix(C(Element.Fire), C(Element.Wind), 0.5f), Shake = 8f,
        });
        Fusion(Element.Fire, Element.Dark, "Адское пламя", new SpellDef
        {
            Kind = SpellKind.Bolt, Damage = 24f, Cost = 28, Count = 3, Spread = 0.7f, Speed = 170f, Life = 2.2f, Explode = true, ExplodeRadius = 40f, Color = GameMath.Mix(C(Element.Fire), C(Element.Dark), 0.55f), Shake = 7f,
        });
        Fusion(Element.Fire, Element.Earth, "Магма-взрыв", new SpellDef
        {
            Kind = SpellKind.Nova, Damage = 32f, Cost = 28, Radius = 90f, Color = GameMath.Mix(C(Element.Fire), C(Element.Earth), 0.5f), Shake = 10f,
        });
        Fusion(Element.Fire, Element.Water, "Взрыв пара", new SpellDef
        {
            Kind = SpellKind.Nova, Damage = 20f, Cost = 26, Radius = 78f, Knockback = 330f, Slow = 0.7f, Color = GameMath.Mix(C(Element.Water), Color.White, 0.4f), Shake = 6f,
        });
        Fusion(Element.Fire, Element.Light, "Священное пламя", new SpellDef
        {
            Kind = SpellKind.Beam, Damage = 30f, Cost = 30, BeamLength = 310f, BeamWidth = 18f, Color = GameMath.Mix(C(Element.Fire), C(Element.Light), 0.5f), Shake = 7f,
        });
        Fusion(Element.Wind, Element.Water, "Ледяная буря", new SpellDef
        {
            Kind = SpellKind.Field, Damage = 26f, Cost = 28, Radius = 60f, Duration = 3.4f, Slow = 0.6f, Color = GameMath.Mix(C(Element.Wind), C(Element.Water), 0.5f), Shake = 5f,
        });
        Fusion(Element.Wind, Element.Earth, "Песчаная буря", new SpellDef
        {
            Kind = SpellKind.Cone, Damage = 24f, Cost = 27, Radius = 100f, Angle = 1.6f, Knockback = 230f, Color = GameMath.Mix(C(Element.Wind), C(Element.Earth), 0.5f), Shake = 6f,
        });
        Fusion(Element.Wind, Element.Dark, "Шёпот пустоты", new SpellDef
        {
            Kind = SpellKind.Bolt, Damage = 20f, Cost = 29, Count = 4, Spread = 0.9f, Speed = 150f, Life = 2.4f, Homing = true, Color = GameMath.Mix(C(Element.Wind), C(Element.Dark), 0.5f), Shake = 5f,
        });
        Fusion(Element.Wind, Element.Light, "Солнечный ветер", new SpellDef
        {
            Kind = SpellKind.Nova, Damage = 18f, Cost = 27, Radius = 86f, Knockback = 270f, Heal = 6f, Color = GameMath.Mix(C(Element.Wind), C(Element.Light), 0.5f), Shake = 5f,
        });
        Fusion(Element.Water, Element.Earth, "Грязевой вал", new SpellDef
        {
            Kind = SpellKind.Cone, Damage = 24f, Cost = 27, Radius = 94f, Angle = 1.5f, Slow = 0.7f, Color = GameMath.Mix(C(Element.Water), C(Element.Earth), 0.5f), Shake = 6f,
        });
        Fusion(Element.Water, Element.Dark, "Чёрный лёд", new SpellDef
        {
            Kind = SpellKind.Rain, Damage = 22f, Cost = 30, Count = 6, Radius = 25f, Delay = 0.1f, Stun = 0.9f, Color = GameMath.Mix(C(Element.Water), C(Element.Dark), 0.5f), Shake = 6f,
        });
        Fusion(Element.Water, Element.Light, "Святой прилив", new SpellDef
        {
            Kind = SpellKind.Nova, Damage = 22f, Cost = 28, Radius = 94f, Heal = 16f, Color = GameMath.Mix(C(Element.Water), C(Element.Light), 0.5f), Shake = 5f,
        });
        Fusion(Element.Earth, Element.Dark, "Бездна", new SpellDef
        {
            Kind = SpellKind.Field, Damage = 28f, Cost = 30, Radius = 54f, Duration = 3.6f, Pull = 180f, Color = GameMath.Mix(C(Element.Earth), C(Element.Dark), 0.5f), Shake = 8f,
        });
        Fusion(Element.Earth, Element.Light, "Сияющий бастион", new SpellDef
        {
            Kind = SpellKind.Nova, Damage = 26f, Cost = 29, Radius = 86f, Stun = 1.2f, Heal = 4f, Color = GameMath.Mix(C(Element.Earth), C(Element.Light), 0.5f), Shake = 6f,
        });
        Fusion(Element.Dark, Element.Light, "Затмение", new SpellDef
        {
            Kind = SpellKind.Beam, Damage = 40f, Cost = 34, BeamLength = 330f, BeamWidth = 26f, SelfDamage = 12f, Color = GameMath.Mix(C(Element.Dark), C(Element.Light), 0.5f), Shake = 14f,
        });

        AssignSheets();
    }

    private static void Add(SpellDef def)
    {
        All.Add(def);
        ByCombo[def.Combo] = def;
    }

    /// <summary>
    /// Подбор анимации по стихии и виду заклинания, когда у него нет
    /// своего листа. Девять листов покрывают все шесть видов.
    /// </summary>
    public static string SheetFor(Element element, SpellKind kind) => kind switch
    {
        SpellKind.Bolt => element switch
        {
            Element.Fire => "fire-bolt",
            Element.Earth => "smoke-bolt",
            Element.Water => "smoke-bolt",
            Element.Dark => "claw-bolt",
            Element.Wind => "claw-bolt",
            _ => "fire-bolt",
        },
        SpellKind.Nova => element switch
        {
            Element.Dark => "arcane-nova",
            Element.Fire => "ring-nova",
            Element.Earth => "ring-nova",
            Element.Water => "shock-ring",
            Element.Light => "shock-ring",
            _ => "shock-ring",
        },
        SpellKind.Field => element == Element.Dark || element == Element.Earth ? "void-field" : "fire-bolt",
        SpellKind.Rain => element == Element.Fire ? "comet-rain" : "shard-rain",
        SpellKind.Cone => element == Element.Earth || element == Element.Wind ? "smoke-bolt" : "claw-bolt",
        SpellKind.Beam => element == Element.Dark ? "arcane-nova" : "shock-ring",
        _ => "fire-bolt",
    };

    /// <summary>
    /// Заклинания, которым анимация подобрана вручную - под них рисовались листы.
    /// Остальные, включая слияния, получают лист автоматически по SheetFor.
    /// </summary>
    private static void AssignSheets()
    {
        Use("FFF", "fire-bolt");      // Огненный шар
        Use("FFFF", "comet-rain");    // Огненный дождь
        Use("LLLL", "shard-rain");    // Звёздный дождь
        Use("DDD", "arcane-nova");    // тёмная бездна
        Use("DDE", "void-field");     // могильная ловушка
        Use("FAE", "ring-nova");      // пекло
        Use("EE", "smoke-bolt");      // каменный кулак
        Use("AAAA", "shock-ring");    // прилив
        Use("WE", "smoke-bolt");      // пыльный ветер
    }

    private static void Use(string combo, string sheet)
    {
        if (ByCombo.TryGetValue(combo, out SpellDef? def)) def.Sheet = sheet;
    }

    private static void Fusion(Element a, Element b, string name, SpellDef def)
    {
        def.Name = name;
        def.Fusion = true;
        def.Element = a;
        def.Score = 6;
        def.FlowGain = 0.3f;
        def.Color = def.Color == Color.White ? Elements.Color(b) : def.Color;
        Element lo = (int)a <= (int)b ? a : b;
        Element hi = (int)a <= (int)b ? b : a;
        Fusions[(lo, hi)] = def;
    }

    public static SpellDef? FindFusion(Element a, Element b)
    {
        if ((int)a > (int)b) (a, b) = (b, a);
        return Fusions.TryGetValue((a, b), out SpellDef? def) ? def : null;
    }

    public static SpellDef? Find(string combo) => ByCombo.TryGetValue(combo, out SpellDef? def) ? def : null;
}
