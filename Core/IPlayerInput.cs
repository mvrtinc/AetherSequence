using System.Numerics;

namespace AetherSequence.Core;

/// <summary>
/// Минимальный контракт ввода, который нужен игроку. Реализуется локальным
/// Input и сетевым вводом соперника - благодаря этому код Player общий.
/// </summary>
internal interface IPlayerInput
{
    Vector2 Move { get; }

    bool Down(InputAction action);

    bool Pressed(InputAction action);

    float HoldTime(InputAction action);
}
