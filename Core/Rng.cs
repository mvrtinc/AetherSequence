using System.Numerics;

namespace AetherSequence.Core;

internal struct Rng
{
    private uint _state;

    public Rng(uint seed)
    {
        _state = seed == 0u ? 0x9E3779B9u : seed;
        for (int i = 0; i < 4; i++) NextUInt();
    }

    public static Rng FromTime()
    {
        ulong t = (ulong)DateTime.UtcNow.Ticks;
        return new Rng((uint)(t ^ (t >> 32)));
    }

    public uint NextUInt()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return _state;
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        int span = maxExclusive - minInclusive;
        if (span <= 0) return minInclusive;
        return minInclusive + (int)(NextUInt() % (uint)span);
    }

    public float NextFloat() => (NextUInt() & 0xFFFFFF) / 16777216f;

    public float Range(float a, float b) => a + NextFloat() * (b - a);

    public bool Chance(float p) => NextFloat() < p;

    public T Pick<T>(IReadOnlyList<T> list) => list[Next(0, list.Count)];

    public Vector2 InsideCircle(float radius)
    {
        float a = NextFloat() * GameMath.Tau;
        float r = MathF.Sqrt(NextFloat()) * radius;
        return new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);
    }

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public uint Hash(uint v)
    {
        v ^= v >> 16;
        v *= 0x7FEB352Du;
        v ^= v >> 15;
        v *= 0x846CA68Bu;
        v ^= v >> 16;
        return v;
    }
}
