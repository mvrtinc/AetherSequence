using System.Buffers.Binary;
using System.Net;
using System.Text;

using System.IO;
using System.Numerics;
using AetherSequence.Combat;
using AetherSequence.Core;
using AetherSequence.Entities;

namespace AetherSequence.Net;

internal enum MsgKind : byte
{
    None = 0,
    Hello,
    Welcome,
    Reject,
    RoomInfo,
    RoomClosed,
    StartMatch,
    InputCmd,
    Snapshot,
    RoundEnd,
    MatchEnd,
    Ping,
    Pong,
}

internal static class NetProtocol
{
    // Версия 4: в Hello, Welcome и снапшот добавлен класс персонажа.
    // Поднимать обязательно: формат сообщений позиционный и не версионируется
    // по частям. Без подъёма старый гость и новая игра договорятся и разъедут
    // поток снапшота - дуэль молча рассыплется. С подъёмом хост честно
    // отклоняет гостя с понятным сообщением, а комнаты другой версии
    // исчезают из списка находок.
    public const int Version = 4;
    public const int DiscoveryPort = 47800;
    public const int GamePort = 47801;
    public const int MaxNickLength = 18;

    public const byte DiscoveryMagic = 0xA5;
    public const byte ProtocolMagic = 0xE7;

    public static void WriteString(BinaryWriter writer, string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) value = string.Empty;
        if (value.Length > maxLength) value = value[..maxLength];

        int bytes = Encoding.UTF8.GetByteCount(value);
        if (bytes > 256) value = value[..Math.Max(0, value.Length - 8)];
        bytes = Encoding.UTF8.GetByteCount(value);
        writer.Write((ushort)bytes);
        Span<byte> buffer = stackalloc byte[256];
        Encoding.UTF8.GetBytes(value, buffer);
        writer.Write(buffer[..bytes]);
    }

    public static string ReadString(BinaryReader reader, int maxLength)
    {
        ushort bytes = reader.ReadUInt16();
        if (bytes > 256) throw new ProtocolViolationException("строка слишком длинная");
        byte[] buffer = reader.ReadBytes(bytes);
        string value = Encoding.UTF8.GetString(buffer);
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    public static IPAddress ParseAddress(uint packed) => new(packed);

    public static uint PackAddress(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        if (bytes.Length != 4) return 0;
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    public static void WriteFloats(BinaryWriter writer, params float[] values)
    {
        foreach (float value in values) writer.Write(value);
    }

    public static float ReadFloat(BinaryReader reader) => reader.ReadSingle();

    /// <summary>Записать состояние обоих игроков и снарядов (используется хостом).</summary>
    public static void WriteSnapshot(BinaryWriter writer, Game game)
    {
        writer.Write(game.DuelTick);
        writer.Write((byte)2);

        WritePeer(writer, game.Player);
        if (game.Foe is not null) WritePeer(writer, game.Foe);

        int shots = Math.Min(game.Projectiles.Count, 255);
        writer.Write((byte)shots);
        for (int i = 0; i < shots; i++)
        {
            Projectile pr = game.Projectiles[i];
            writer.Write(pr.Pos.X);
            writer.Write(pr.Pos.Y);
            writer.Write(pr.Vel.X);
            writer.Write(pr.Vel.Y);
            writer.Write(pr.Radius);
            writer.Write(pr.Damage);
            writer.Write((byte)pr.Element);
            writer.Write((byte)pr.Style);
            writer.Write(pr.Hostile);
            writer.Write((byte)Math.Clamp(pr.Owner, 0, 1));
            writer.Write(pr.Life);
            writer.Write(pr.Size);
            writer.Write(pr.Length);
            writer.Write(pr.Color.ToArgb());
        }
    }

    private static void WritePeer(BinaryWriter writer, Player p)
    {
     writer.Write((byte)(ReferenceEquals(p, Game.CurrentHostPlayer) ? 0 : 1));
        writer.Write(p.Pos.X);
    writer.Write(p.Pos.Y);
        writer.Write(p.Hp);
        writer.Write(p.MaxHp);
        writer.Write(p.Mana);
        writer.Write(p.AimAngle);
        writer.Write(p.Alive);
        writer.Write(p.Dashing);
        writer.Write((byte)GameMath.ClampI(p.SelectedRune, 0, 5));
    writer.Write(p.HitFlash);
        writer.Write((byte)GameMath.ClampI((int)(p.Flow * 10f), 0, 10));

        // Класс пишем последним: добавление поля в конец не сдвигает
        // остальные, и отлаживать рассинхрон проще.
        writer.Write((byte)CharacterClasses.Clamp((int)p.Class));
    }
}
