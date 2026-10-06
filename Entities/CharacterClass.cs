using System.Drawing;
using AetherSequence.Art;
using AetherSequence.Core;

namespace AetherSequence.Entities;

/// <summary>Класс персонажа. Боевая механика у классов общая, различается вид и свет.</summary>
internal enum PlayerClass
{
    Mage = 0,
    Archer = 1,
}

/// <summary>Чем засветляет комнату персонаж.</summary>
internal enum BeaconKind
{
    /// <summary>Каменный постамент с кристаллом.</summary>
    Crystal,

    /// <summary>Кострище из камней и поленьев.</summary>
    Campfire,
}

/// <summary>
/// Реестр классов. Всё, что отличает одного персонажа от другого, описано
/// здесь: числа, спрайты, источник света и вид маяка освещения.
///
/// Классы намеренно не меняют боевые числа заклинаний - оба кастуют одни и
/// те же 38 умений через общий буфер стихий. Разница только в том, чем
/// персонаж светит и как выглядит, иначе "выбор класса" ничего не значил бы.
/// </summary>
internal static class CharacterClasses
{
    public const int Count = 2;

    public static readonly string[] Names = { "МАГ", "СТРЕЛОК" };

    /// <summary>
    /// Короткая строка под портретом в меню выбора. Держим в пределах
    /// 21 символа: панель узкая, а Text не переносит строки и не ужимает.
    /// </summary>
    public static readonly string[] Summaries =
    {
        "ЭФИР И РЕЗОНАНС",
        "ОГОНЬ И ЛУК",
    };

    /// <summary>Строка подсказки на титульном экране: что зажигает этот класс.</summary>
    public static readonly string[] BeaconNames = { "КРИСТАЛЛ ОСВЕЩЕНИЯ", "КОСТЁР" };

    public static string Name(PlayerClass c) => Names[(int)c];

    public static string Summary(PlayerClass c) => Summaries[(int)c];

    public static string BeaconName(PlayerClass c) => BeaconNames[(int)c];

    public static PlayerClass Clamp(int value) => (PlayerClass)GameMath.ClampI(value, 0, Count - 1);

    /// <summary>
    /// Следующий класс по кругу. Именно по кругу, а не с ограничением:
    /// при клампе стрелка «вправо» на последнем классе ничего не делала,
    /// и переключить класс обратно было бы нечем.
    /// </summary>
    public static PlayerClass Next(PlayerClass c, int direction)
    {
        int next = (int)c + (direction < 0 ? -1 : 1);
        return Clamp(((next % Count) + Count) % Count);
    }

    /// <summary>Спрайт тела: у обоих классов одинаковый размер 16x20.</summary>
    public static Sprite Body(PlayerClass c)
        => c == PlayerClass.Archer ? Sprites.ArcherBody : Sprites.MageBody;

    /// <summary>Плащ для контрового света. У стрелка он короче и плотнее.</summary>
    public static Sprite Cape(PlayerClass c)
        => c == PlayerClass.Archer ? Sprites.ArcherCape : Sprites.MageCape;

    public static BeaconKind Beacon(PlayerClass c)
        => c == PlayerClass.Archer ? BeaconKind.Campfire : BeaconKind.Crystal;

    /// <summary>
    /// Базовый радиус ореола героя. У мага свет ровный и исходит из посоха,
    /// у стрелка - из факела в руке, он треплеться и бьёт дальше.
    /// </summary>
    public static float AuraRadius(PlayerClass c) => c == PlayerClass.Archer ? 62f : 58f;

    /// <summary>Радиус ореола во время зарядки маяка: оба класса видят хуже.</summary>
    public static float ChargingRadius(PlayerClass c) => c == PlayerClass.Archer ? 36f : 34f;

    /// <summary>Цвет собственного света, если стихия не окрашивает его.</summary>
    public static Color AuraColor(PlayerClass c)
        => c == PlayerClass.Archer ? GameMath.Rgb(255, 168, 78) : GameMath.Rgb(150, 220, 255);

    /// <summary>
    /// Доля стихии в цвете ореола. У факела огонь своё и стихию он почти не
    /// перебивает, у посоха наоборот - кристалл перекрашивается кастом.
    /// </summary>
    public static float AuraElementMix(PlayerClass c) => c == PlayerClass.Archer ? 0.18f : 0.35f;

    /// <summary>
    /// Амплитуда мерцания факела. У мага ноль: сфера посоха стабильна.
    /// Именно по этому признаку в тесте отличаются классы на свету.
    /// </summary>
    public static float AuraFlicker(PlayerClass c) => c == PlayerClass.Archer ? 0.09f : 0f;

    /// <summary>Цвет света зажжённого маяка.</summary>
    public static Color BeaconColor(PlayerClass c)
        => c == PlayerClass.Archer ? GameMath.Rgb(255, 150, 62) : GameMath.Rgb(150, 220, 255);

    /// <summary>Мерцание света маяка: костёр дышит, кристалл горит ровно.</summary>
    public static float BeaconFlicker(PlayerClass c) => c == PlayerClass.Archer ? 0.11f : 0f;

    /// <summary>Максимальное здоровье класса.</summary>
    public static float MaxHp(PlayerClass c) => c == PlayerClass.Archer ? 108f : 120f;

    /// <summary>Перезарядка рывка, секунды.</summary>
    public static float DashCooldown(PlayerClass c) => c == PlayerClass.Archer ? 0.40f : 0.52f;

    /// <summary>Шанс критического удара.</summary>
    public static float CritChance(PlayerClass c) => c == PlayerClass.Archer ? 0.14f : 0.05f;

    /// <summary>Регенерация маны в секунду.</summary>
    public static float ManaRegen(PlayerClass c) => c == PlayerClass.Archer ? 16f : 18f;

    /// <summary>
 /// Числа класса. Значения подобраны так, чтобы читались как два разных
    /// стиля игры, а не как "один лучше другого": маг живучее и экономнее по
 /// рывкам, стрелок быстрее уходит из-под удара и чаще crit'ит.
    /// </summary>
    public static void Apply(Player p)
    {
        p.MaxHp = MaxHp(p.Class);
        p.DashCooldown = DashCooldown(p.Class);
        p.CritChance = CritChance(p.Class);
        p.ManaRegen = ManaRegen(p.Class);
        p.Hp = p.MaxHp;
    }
}
