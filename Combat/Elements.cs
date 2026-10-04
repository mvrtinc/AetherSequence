using System.Drawing;
using AetherSequence.Core;

namespace AetherSequence.Combat;

internal enum Element
{
    Fire,
    Wind,
    Water,
    Earth,
    Dark,
    Light,
}

internal static class Elements
{
    public static readonly Color[] Colors =
    {
        GameMath.Rgb(255, 122, 48),
        GameMath.Rgb(150, 240, 220),
        GameMath.Rgb(72, 150, 255),
        GameMath.Rgb(196, 150, 70),
        GameMath.Rgb(168, 92, 255),
        GameMath.Rgb(255, 226, 130),
    };

    public static readonly string[] Names =
    {
        "ОГОНЬ",
        "ВЕТЕР",
        "ВОДА",
        "ЗЕМЛЯ",
        "ТЬМА",
        "СВЕТ",
    };

    public static readonly string[] Glyphs = { "ОГ", "ВЕ", "ВО", "ЗЕ", "ТЬ", "СВ" };

    public static Color Color(Element e) => Colors[(int)e];

    public static string Name(Element e) => Names[(int)e];

    public static string Glyph(Element e) => Glyphs[(int)e];
}

internal static class Runes
{
    public const char Fire = 'F';
    public const char Wind = 'W';
    public const char Water = 'A';
    public const char Earth = 'E';
    public const char Dark = 'D';
    public const char Light = 'L';

    public static readonly Element[] Order = { Element.Fire, Element.Wind, Element.Water, Element.Earth, Element.Dark, Element.Light };

    public static char Glyph(Element e) => e switch
    {
        Element.Fire => Fire,
        Element.Wind => Wind,
        Element.Water => Water,
        Element.Earth => Earth,
        Element.Dark => Dark,
        _ => Light,
    };

    public static string AsString(IEnumerable<Element> elements)
    {
        System.Text.StringBuilder sb = new();
        foreach (Element e in elements) sb.Append(Glyph(e));
        return sb.ToString();
    }

    public static int KeyToSlot(Keys key) => key switch
    {
        Keys.D1 => 0,
        Keys.D2 => 1,
        Keys.D3 => 2,
        Keys.D4 => 3,
        Keys.D5 => 4,
        Keys.D6 => 5,
        _ => -1,
    };
}
