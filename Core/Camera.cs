using System.Numerics;

namespace AetherSequence.Core;

internal sealed class Camera
{
    private float _time;

    public Vector2 Position;

    public float Shake;

    public Vector2 Offset => new(
        MathF.Sin(_time * 83.7f) * Shake + MathF.Sin(_time * 31.1f) * Shake * 0.4f,
        MathF.Cos(_time * 71.3f) * Shake + MathF.Cos(_time * 27.7f) * Shake * 0.4f);

    public void Add(float amount) => Shake = MathF.Min(Shake + amount, 13f);

    public void Update(float dt)
    {
        _time += dt;
        Shake = GameMath.Approach(Shake, 0f, dt * 30f);
    }

    public void SnapTo(Vector2 target, Vector2 view, Vector2 world)
    {
        Position = Clamp(target - view * 0.5f, Vector2.Zero, world - view);
    }

    public void Follow(Vector2 target, Vector2 view, Vector2 world, float dt, float lead = 26f)
    {
        Vector2 desired = target + GameMath.Normalized(target - Position - view * 0.5f) * lead - view * 0.5f;
        desired = Clamp(desired, Vector2.Zero, world - view);
        float k = 1f - MathF.Exp(-dt * 9f);
        Position += (desired - Position) * k;
    }

    public static Vector2 Clamp(Vector2 v, Vector2 min, Vector2 max)
        => new(Mathf(v.X, min.X, max.X), Mathf(v.Y, min.Y, max.Y));

    private static float Mathf(float v, float min, float max) => v < min ? min : v > max ? max : v;
}
