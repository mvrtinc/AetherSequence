using System.Drawing;
using AetherSequence.Art;
using AetherSequence.Combat;
using AetherSequence.Core;
using AetherSequence.Entities;

namespace AetherSequence.Progression;

internal sealed class CardDef
{
    public string Name = string.Empty;
    public string Desc = string.Empty;
    public string Rarity = "Common";
    public Color Color = GameMath.Rgb(159, 179, 200);
    public Action<Player> Apply = _ => { };
}

internal static class Cards
{
    public static readonly List<CardDef> All = new();

    private static void Add(CardDef card) => All.Add(card);

    static Cards()
    {
        Add(new CardDef
        {
            Name = "Живучесть",
            Desc = "+35 к максимуму здоровья",
            Apply = p =>
            {
                p.MaxHp += 35f;
                p.Hp += 35f;
            },
        });
        Add(new CardDef
        {
            Name = "Глубокий колодец",
            Desc = "+25 к максимуму маны",
            Apply = p =>
            {
                p.MaxMana += 25f;
                p.Mana = MathF.Min(p.MaxMana, p.Mana + 25f);
            },
        });
        Add(new CardDef
        {
            Name = "Быстрый поток",
            Desc = "+3 к восстановлению маны в секунду",
            Apply = p => p.ManaRegen += 3f,
        });
        Add(new CardDef
        {
            Name = "Огненное сердце",
            Desc = "+18% к урону заклинаний",
            Apply = p => p.DamageMul += 0.18f,
        });
        Add(new CardDef
        {
            Name = "Ученик мастера",
            Desc = "+20% получаемого опыта",
            Apply = p => p.XpMul += 0.2f,
        });
        Add(new CardDef
        {
            Name = "Целитель",
            Desc = "Немедленно восстановить 45 здоровья",
            Apply = p => p.Heal(45f, false),
        });
        Add(new CardDef
        {
            Name = "Лёгкий рывок",
            Desc = "-18% к перезарядке рывка",
            Apply = p => p.DashCooldown *= 0.82f,
        });
        Add(new CardDef
        {
            Name = "Кровавый ритуал",
            Desc = "Убийства восстанавливают 2.5 маны",
            Apply = p => p.ManaOnKill += 2.5f,
        });
        Add(new CardDef
        {
            Name = "Стеклянный шар",
            Desc = "+28% урона, но -22 здоровья",
            Apply = p =>
            {
                p.DamageMul += 0.28f;
                p.MaxHp = MathF.Max(30f, p.MaxHp - 22f);
                p.Hp = MathF.Min(p.Hp, p.MaxHp);
            },
        });
        Add(new CardDef
        {
            Name = "Скупой маг",
            Desc = "-9% к стоимости заклинаний",
            Apply = p => p.CostMul = MathF.Max(0.4f, p.CostMul - 0.09f),
        });
        Add(new CardDef
        {
            Name = "Дыхание эфира",
            Desc = "Рывок возвращает 5 маны",
            Apply = p => p.ManaOnDash += 5f,
        });

        Add(new CardDef
        {
            Name = "Резонанс",
            Desc = "Окно смешивания стихий +0.22с",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p => p.ResonanceWindow += 0.22f,
        });
        Add(new CardDef
        {
            Name = "Руна Земли",
            Desc = "Открывает стихию Земли (клавиша 4)",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p => p.RuneUnlocked[(int)Element.Earth] = true,
        });
        Add(new CardDef
        {
            Name = "Двойной залп",
            Desc = "Снарядные заклинания выпускают +1 снаряд",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p => p.ExtraBolts += 1,
        });
        Add(new CardDef
        {
            Name = "Критический поток",
            Desc = "+12% шанса критического удара (x2 урона)",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p => p.CritChance += 0.12f,
        });
        Add(new CardDef
        {
            Name = "Вампиризм",
            Desc = "Восстанавливает 5% здоровья от урона",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p => p.Lifesteal += 0.05f,
        });
        Add(new CardDef
        {
            Name = "Магический барьер",
            Desc = "-15% получаемого урона",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p => p.DamageTakenMul = MathF.Max(0.35f, p.DamageTakenMul - 0.15f),
        });
        Add(new CardDef
        {
            Name = "Катализатор",
            Desc = "+45% урона резонансных фузий",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p => p.FusionDamageMul += 0.45f,
        });
        Add(new CardDef
        {
            Name = "Тёмная аура",
            Desc = "Враги в радиусе 40 получают 7 урона в секунду",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p =>
            {
                p.BurnAura += 7f;
                p.BurnAuraRadius = 40f;
            },
        });
        Add(new CardDef
        {
            Name = "Фазовый шаг",
            Desc = "-30% перезарядка рывка, рывок дешевле",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p =>
            {
                p.DashCooldown *= 0.7f;
                p.DashCost = MathF.Max(0f, p.DashCost - 1.5f);
            },
        });
        Add(new CardDef
        {
            Name = "Жажда силы",
            Desc = "+7 маны в секунду, -15 к максимуму маны",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p =>
            {
                p.ManaRegen += 7f;
                p.MaxMana = MathF.Max(40f, p.MaxMana - 15f);
                p.Mana = MathF.Min(p.Mana, p.MaxMana);
            },
        });
        Add(new CardDef
        {
            Name = "Точность",
            Desc = "+14% к урону заклинаний",
            Rarity = "Uncommon",
            Color = GameMath.Rgb(78, 201, 176),
            Apply = p => p.DamageMul += 0.14f,
        });

        Add(new CardDef
        {
            Name = "Руна Света",
            Desc = "Открывает стихию Света (клавиша 6)",
            Rarity = "Rare",
            Color = Palette.Gold,
            Apply = p => p.RuneUnlocked[(int)Element.Light] = true,
        });
        Add(new CardDef
        {
            Name = "Сердце Аэтера",
            Desc = "Восполняет ману, +20 к её максимуму",
            Rarity = "Rare",
            Color = Palette.Gold,
            Apply = p =>
            {
                p.MaxMana += 20f;
                p.Mana = p.MaxMana;
            },
        });
        Add(new CardDef
        {
            Name = "Мастер резонанса",
            Desc = "+0.35с окна фузий и +30% их урона",
            Rarity = "Rare",
            Color = Palette.Gold,
            Apply = p =>
            {
                p.ResonanceWindow += 0.35f;
                p.FusionDamageMul += 0.3f;
            },
        });
        Add(new CardDef
        {
            Name = "Голодание",
            Desc = "+20% урона, +40 маны, -3 реген маны",
            Rarity = "Rare",
            Color = Palette.Gold,
            Apply = p =>
            {
                p.DamageMul += 0.2f;
                p.MaxMana += 40f;
                p.ManaRegen = MathF.Max(2f, p.ManaRegen - 3f);
                p.Mana = MathF.Min(p.MaxMana, p.Mana + 40f);
            },
        });
        Add(new CardDef
        {
            Name = "Абсолютная воля",
            Desc = "-25% получаемого урона, +25 здоровья",
            Rarity = "Rare",
            Color = Palette.Gold,
            Apply = p =>
            {
                p.DamageTakenMul = MathF.Max(0.3f, p.DamageTakenMul - 0.25f);
                p.MaxHp += 25f;
            },
        });
        Add(new CardDef
        {
            Name = "Скорострельность",
            Desc = "-20% к перезарядке рывка и задержкам",
            Rarity = "Rare",
            Color = Palette.Gold,
            Apply = p => p.SpellCooldownMul = MathF.Max(0.4f, p.SpellCooldownMul - 0.2f),
        });
        Add(new CardDef
        {
            Name = "Второе сердце",
            Desc = "+60 к максимуму здоровья",
            Rarity = "Rare",
            Color = Palette.Gold,
            Apply = p =>
            {
                p.MaxHp += 60f;
                p.Hp = p.MaxHp;
            },
        });
    }

    public static List<CardDef> Roll(Rng rng, int count, Func<CardDef, bool>? filter = null)
    {
        List<CardDef> pool = new();
        foreach (CardDef card in All)
        {
            if (filter is not null && !filter(card)) continue;
            pool.Add(card);
        }
        List<CardDef> result = new();
        while (result.Count < count && pool.Count > 0)
        {
            CardDef? chosen = null;
            int guard = 0;
            while (guard++ < 40)
            {
                CardDef candidate = WeightedPick(rng, pool);
                if (!result.Contains(candidate))
                {
                    chosen = candidate;
                    break;
                }
            }
            chosen ??= pool[rng.Next(0, pool.Count)];
            result.Add(chosen);
            pool.Remove(chosen);
        }
        return result;
    }

    private static CardDef WeightedPick(Rng rng, List<CardDef> pool)
    {
        int total = 0;
        foreach (CardDef card in pool) total += Weight(card);
        int roll = rng.Next(0, Math.Max(1, total));
        foreach (CardDef card in pool)
        {
            roll -= Weight(card);
            if (roll < 0) return card;
        }
        return pool[pool.Count - 1];
    }

    private static int Weight(CardDef card) => card.Rarity switch
    {
        "Rare" => 8,
        "Uncommon" => 24,
        _ => 48,
    };
}
