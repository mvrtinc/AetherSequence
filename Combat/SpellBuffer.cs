using System.Text;
using AetherSequence.Core;

namespace AetherSequence.Combat;

internal sealed class SpellBuffer
{
    public const int Capacity = 4;

    private static readonly HashSet<string> ExtendablePrefixes = BuildPrefixes();

    private readonly List<Element> _runes = new();
    private SpellDef? _pending;
    private int _pendingLen;

    public IReadOnlyList<Element> Sequence => _runes;

    public int RuneCount => _runes.Count;

    public Element? LastElement { get; private set; }

    public float ResonanceLeft { get; private set; }

    public float Window { get; set; } = 0.55f;

    public float CommitMax { get; set; } = 0.17f;

    public float CommitLeft { get; private set; }

    public bool HasPending => _pending is not null;

    public SpellDef? PendingSpell => _pending;

    public float ResonanceRatio => Window <= 0f ? 0f : GameMath.Clamp(ResonanceLeft / Window, 0f, 1f);

    public float CommitRatio => CommitMax <= 0f ? 0f : GameMath.Clamp(CommitLeft / CommitMax, 0f, 1f);

    public string ComboText
    {
        get
        {
            if (_runes.Count == 0) return string.Empty;
            StringBuilder sb = new(_runes.Count);
            foreach (Element e in _runes) sb.Append(Runes.Glyph(e));
            return sb.ToString();
        }
    }

    private static HashSet<string> BuildPrefixes()
    {
        HashSet<string> set = new(StringComparer.Ordinal);
        foreach (SpellDef def in SpellDB.All)
        {
            for (int len = 1; len < def.Combo.Length; len++)
            {
                set.Add(def.Combo[..len]);
            }
        }
        return set;
    }

    public bool Push(Element element, out SpellDef? spell, out bool fusion)
    {
        CommitLeft = 0f;
        _pending = null;
        _runes.Add(element);
        if (_runes.Count > Capacity) _runes.RemoveAt(0);
        Evaluate(out spell, out fusion);
        return spell is not null;
    }

    public bool Update(float dt, out SpellDef? spell, out bool fusion)
    {
        spell = null;
        fusion = false;
        ResonanceLeft -= dt;
        if (ResonanceLeft < 0f) ResonanceLeft = 0f;
        if (CommitLeft <= 0f) return false;
        CommitLeft -= dt;
        if (CommitLeft > 0f) return false;
        CommitLeft = 0f;
        return FirePending(out spell, out fusion);
    }

    public bool Flush(out SpellDef? spell, out bool fusion)
    {
        CommitLeft = 0f;
        if (_pending is null)
        {
            _runes.Clear();
            spell = null;
            fusion = false;
            return false;
        }
        return FirePending(out spell, out fusion);
    }

    private bool FirePending(out SpellDef? spell, out bool fusion)
    {
        fusion = false;
        SpellDef pending = _pending!;
        if (_pendingLen > 0 && _pendingLen <= _runes.Count)
        {
            _runes.RemoveRange(_runes.Count - _pendingLen, _pendingLen);
        }
        _runes.Clear();
        _pending = null;
        Resolve(pending, out spell, out fusion);
        return true;
    }

    public void Clear()
    {
        _runes.Clear();
        _pending = null;
        CommitLeft = 0f;
        ResonanceLeft = 0f;
        LastElement = null;
    }

    private void Evaluate(out SpellDef? spell, out bool fusion)
    {
        spell = null;
        fusion = false;
        while (_runes.Count > 0)
        {
            int count = _runes.Count;
            bool matched = false;
            for (int len = count; len >= 1; len--)
            {
                if (!TryBuild(count - len, len, out string combo)) continue;
                SpellDef? def = SpellDB.Find(combo);
                if (def is null) continue;
                matched = true;
                if (!ExtendablePrefixes.Contains(combo) || count >= Capacity)
                {
                    _runes.RemoveRange(count - len, len);
                    _pending = null;
                    CommitLeft = 0f;
                    Resolve(def, out spell, out fusion);
                    return;
                }
                _pending = def;
                _pendingLen = len;
                CommitLeft = CommitMax;
                return;
            }
            if (matched) return;
            _runes.RemoveAt(0);
        }
    }

    private bool TryBuild(int start, int len, out string combo)
    {
        StringBuilder sb = new(len);
        for (int i = 0; i < len; i++) sb.Append(Runes.Glyph(_runes[start + i]));
        combo = sb.ToString();
        return combo.Length == len;
    }

    private void Resolve(SpellDef def, out SpellDef? spell, out bool fusion)
    {
        fusion = false;
        SpellDef? fusionSpell = null;
        if (LastElement.HasValue && ResonanceLeft > 0f && LastElement.Value != def.Element)
        {
            fusionSpell = SpellDB.FindFusion(LastElement.Value, def.Element);
        }
        if (fusionSpell is not null)
        {
            spell = fusionSpell;
            fusion = true;
            LastElement = fusionSpell.Element;
            ResonanceLeft = Window * 0.55f;
        }
        else
        {
            spell = def;
            LastElement = def.Element;
            ResonanceLeft = Window;
        }
    }
}
