using NAudio.Wave;
using AetherSequence.Core;

namespace AetherSequence.Audio;

internal sealed class AudioMixer : ISampleProvider
{
    private const int MaxVoices = 48;
    private const float FadeRate = 0.00005f;

    private struct Voice
    {
        public float[]? Data;
        public double Position;
        public float Step;
        public float Gain;
        public float PanLeft;
        public float PanRight;
        public bool Active;
    }

    private readonly Voice[] _voices = new Voice[MaxVoices];
    private readonly object _gate = new();

    private float[]? _music;
    private float[]? _pendingMusic;
    private float _musicPosition;
    private float _fade;
    private float _fadeTarget;
    private float _musicVolume = 0.55f;
    private float _sfxVolume = 0.8f;
    private float _masterVolume = 0.9f;
    private float _musicStep = 1f;
    private int _nextVoice;

    public AudioMixer()
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Synth.Rate, 2);
    }

    public void SetMusicRate(int rate)
    {
        lock (_gate)
        {
            _musicStep = (float)rate / Synth.Rate;
        }
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_gate)
        {
        int frames = count / 2;
        for (int frame = 0; frame < frames; frame++)
            {
                float musicLeft = 0f;
                float musicRight = 0f;

                if (_music is not null && _fade > 0.0005f)
                {
                    int loop = _music.Length;
                    int f = (int)_musicPosition;
                    int f1 = f + 2 < loop ? f + 2 : 0;
                    float t = _musicPosition - f;
                    musicLeft = (_music[f] + ((_music[f1] - _music[f]) * t)) * _fade;
                    musicRight = (_music[f + 1] + ((_music[f1 + 1] - _music[f + 1]) * t)) * _fade;
                    _musicPosition += 2f * _musicStep;
                    if (_musicPosition >= loop) _musicPosition -= loop;
                    _fade += (_fadeTarget - _fade) * FadeRate;
                }
                else if (_pendingMusic is not null)
                {
                    _music = _pendingMusic;
                    _pendingMusic = null;
                    _musicPosition = 0f;
                    _fade = 0f;
                    _fadeTarget = 1f;
                }
                else
                {
                    _fade = 0f;
                }

                float sfxLeft = 0f;
                float sfxRight = 0f;
                for (int v = 0; v < _voices.Length; v++)
                {
                    Voice voice = _voices[v];
                    if (!voice.Active || voice.Data is null) continue;
                    int p = (int)voice.Position;
                    if (p >= voice.Data.Length)
                    {
                        voice.Active = false;
                        voice.Data = null;
                        _voices[v] = voice;
                        continue;
                    }
                    float s = voice.Data[p] * voice.Gain;
                    sfxLeft += s * voice.PanLeft;
                    sfxRight += s * voice.PanRight;
                    voice.Position += voice.Step;
                    _voices[v] = voice;
                }

                int i = offset + (frame * 2);
                if (i < count) buffer[i] = (musicLeft * _musicVolume) + (sfxLeft * _sfxVolume * _masterVolume);
                if (i + 1 < count) buffer[i + 1] = (musicRight * _musicVolume) + (sfxRight * _sfxVolume * _masterVolume);
            }
        }
        return count;
    }

    public void PlayVoice(float[] data, float gain, float pitch, float pan)
    {
        if (data.Length == 0) return;
        lock (_gate)
        {
            int slot = -1;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].Active)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
            {
                slot = _nextVoice;
                _nextVoice = (_nextVoice + 1) % _voices.Length;
            }

            float angle = (pan + 1f) * 0.25f * MathF.PI;
            _voices[slot] = new Voice
            {
                Data = data,
                Position = 0,
                Step = GameMath.Clamp(pitch, 0.4f, 2.5f),
                Gain = gain,
                PanLeft = MathF.Cos(angle) * 1.4142f,
                PanRight = MathF.Sin(angle) * 1.4142f,
                Active = true,
            };
        }
    }

    public void SetMusicData(float[]? data)
    {
        lock (_gate)
        {
            if (data is null)
            {
                _pendingMusic = null;
                _fadeTarget = 0f;
                return;
            }
            if (_music is null)
            {
                _music = data;
                _musicPosition = 0;
                _fade = 0f;
                _fadeTarget = 1f;
                return;
            }
            _pendingMusic = data;
            _fadeTarget = 0f;
        }
    }

    public void SetVolumes(float master, float music, float sfx)
    {
        lock (_gate)
        {
            _masterVolume = GameMath.Clamp(master, 0f, 1f);
            _musicVolume = GameMath.Clamp(music, 0f, 1f);
            _sfxVolume = GameMath.Clamp(sfx, 0f, 1f);
        }
    }

    public void StopVoices()
    {
        lock (_gate)
        {
            for (int i = 0; i < _voices.Length; i++) _voices[i] = default;
        }
    }
}
