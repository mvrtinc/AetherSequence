using System.Collections.Concurrent;
using System.Numerics;
using NAudio.Wave;
using AetherSequence.Combat;
using AetherSequence.Core;

namespace AetherSequence.Audio;

internal static class AudioSystem
{
    private const int MusicRate = 22050;

    private static readonly ConcurrentDictionary<Sfx, float[]> SfxCache = new();
    private static readonly ConcurrentDictionary<MusicTrack, float[]> MusicCache = new();

    private static AudioMixer? _mixer;
    private static WaveOutEvent? _output;
    private static MusicTrack _currentTrack = MusicTrack.None;
    private static bool _initializing;
    private static bool _initialized;
    private static float _lastMusicVolume = -1f;
    private static float _lastSfxVolume = -1f;
    private static float _lastMasterVolume = -1f;
    private static readonly long[] LastPlay = new long[32];
    private const long MinIntervalMs = 34;

    public static bool Available => _initialized;

    public static string DeviceName { get; private set; } = "неизвестно";

    public static MusicTrack CurrentTrack => _currentTrack;

    public static void Init()
    {
        if (_initialized || _initializing) return;
        _initializing = true;
        try
        {
            AudioMixer mixer = new();
            WaveOutEvent output = new()
            {
                DesiredLatency = 120,
                NumberOfBuffers = 3,
            };
            output.Init(mixer);
            output.Play();
            _mixer = mixer;
            mixer.SetMusicRate(MusicRate);
            _output = output;
            _initialized = true;
        }
        catch
        {
            _mixer = null;
            _output = null;
            _initialized = false;
        }
        finally
        {
            _initializing = false;
        }
    }

    public static void Shutdown()
    {
        try
        {
            _output?.Stop();
            _output?.Dispose();
            _mixer?.StopVoices();
        }
        catch
        {
        }
        _output = null;
        _mixer = null;
        _initialized = false;
        _currentTrack = MusicTrack.None;
    }

    public static void Play(Sfx id, float gain = 1f, float pitch = 1f, float pan = 0f)
    {
        AudioMixer? mixer = _mixer;
        if (mixer is null) return;
        long now = Environment.TickCount64;
        int index = (int)id;
        if (now - LastPlay[index] < MinIntervalMs) return;
        LastPlay[index] = now;
        float[] data = SfxCache.GetOrAdd(id, SfxBank.Render);
        mixer.PlayVoice(data, gain, pitch, pan);
    }

    public static void PlayAt(Sfx id, Vector2 worldPosition, Vector2 listener, float gain = 1f, float pitch = 1f)
    {
        float distance = Vector2.Distance(worldPosition, listener);
        float attenuation = 1f / (1f + (distance / 170f) * (distance / 170f));
        float pan = GameMath.Clamp((worldPosition.X - listener.X) / 190f, -1f, 1f);
        Play(id, gain * MathF.Max(0.12f, attenuation), pitch, pan);
    }

    public static void SetMusic(MusicTrack track)
    {
        if (_currentTrack == track && _mixer is not null) return;
        _currentTrack = track;
        AudioMixer? mixer = _mixer;
        if (mixer is null) return;
        if (track == MusicTrack.None)
        {
            mixer.SetMusicData(null);
            return;
        }
        mixer.SetMusicData(MusicCache.GetOrAdd(track, t => MusicBank.RenderStereo(t, MusicRate)));
    }

    public static void SetVolumes(float master, float music, float sfx)
    {
        if (MathF.Abs(master - _lastMasterVolume) < 0.001f
            && MathF.Abs(music - _lastMusicVolume) < 0.001f
            && MathF.Abs(sfx - _lastSfxVolume) < 0.001f)
        {
            return;
        }
        _lastMasterVolume = master;
        _lastMusicVolume = music;
        _lastSfxVolume = sfx;
        _mixer?.SetVolumes(master, music, sfx);
    }

    public static float[] RenderSfx(Sfx id) => SfxBank.Render(id);

    public static float[] RenderMusic(MusicTrack track) => MusicBank.RenderStereo(track, MusicRate);

    public static void Warmup()
    {
        if (!_initialized) return;
        MusicCache.GetOrAdd(MusicTrack.Menu, t => MusicBank.RenderStereo(t, MusicRate));
        SfxCache.GetOrAdd(Sfx.UiMove, SfxBank.Render);
    }

    public static void WarmupBackground()
    {
        if (!_initialized) return;
        MusicTrack[] tracks = { MusicTrack.Dungeon, MusicTrack.Boss, MusicTrack.Victory };
        foreach (MusicTrack track in tracks)
        {
            MusicCache.GetOrAdd(track, t => MusicBank.RenderStereo(t, MusicRate));
        }

        Sfx[] sounds =
        {
            Sfx.Spark, Sfx.CastFire, Sfx.CastWind, Sfx.CastWater, Sfx.CastEarth, Sfx.CastDark,
            Sfx.CastLight, Sfx.Fusion, Sfx.Dash, Sfx.PlayerHurt, Sfx.EnemyHurt, Sfx.EnemyDeath,
            Sfx.LevelUp, Sfx.CardPick, Sfx.Pickup, Sfx.PickupMana, Sfx.PickupHealth, Sfx.Portal,
            Sfx.Telegraph, Sfx.BossRoar, Sfx.BossPhase, Sfx.Victory, Sfx.Death, Sfx.UiSelect, Sfx.UiBack,
        };
        foreach (Sfx id in sounds) SfxCache.GetOrAdd(id, SfxBank.Render);
    }

    public static Sfx ElementSfx(Element element) => element switch
    {
        Element.Fire => Sfx.CastFire,
        Element.Wind => Sfx.CastWind,
        Element.Water => Sfx.CastWater,
        Element.Earth => Sfx.CastEarth,
        Element.Dark => Sfx.CastDark,
        _ => Sfx.CastLight,
    };

    public static float ElementPitch(Element element) => element switch
    {
        Element.Fire => 1f,
        Element.Wind => 1.12f,
        Element.Water => 0.92f,
        Element.Earth => 0.85f,
        Element.Dark => 0.8f,
        _ => 1.2f,
    };
}
