// Audio2D：零音檔的「程序化音樂 + 音效」。
//  · 音樂：AudioStreamGenerator 即時合成氛圍音（低音 + 弦墊 + 琶音，Am→F→C→G 和弦循環）
//  · 音效：啟動時把合成波形包成 AudioStreamWav，用播放器池播放
// 音量由 Settings2D 控制；headless 模式跳過（無音訊裝置）。
using System;
using System.Collections.Generic;
using Godot;

namespace CoreFactory.View2D
{
    public sealed partial class Audio2D : Node
    {
        private const float RATE = 44100f;
        private const float Tau = MathF.PI * 2f;

        // 音樂
        private AudioStreamPlayer _music;
        private AudioStreamGeneratorPlayback _playback;
        private float _musicTime;

        // 音效池
        private readonly Dictionary<string, AudioStreamWav> _sfx = new();
        private readonly List<AudioStreamPlayer> _pool = new();
        private int _poolIdx;

        private static readonly int[][] Chords =
        {
            new[] { 45, 57, 60, 64 },   // Am
            new[] { 41, 53, 57, 60 },   // F
            new[] { 48, 60, 64, 67 },   // C
            new[] { 43, 55, 59, 62 },   // G
        };
        private const float ChordLen = 3.2f;
        private const float ArpStep = ChordLen / 8f;

        public void Setup()
        {
            ProcessMode = ProcessModeEnum.Always;   // 暫停時音樂也繼續（避免中斷）
            if (DisplayServer.GetName() == "headless") return;

            // ── 音樂 ──
            var gen = new AudioStreamGenerator { MixRate = RATE, BufferLength = 1.0f };
            _music = new AudioStreamPlayer { Stream = gen, Autoplay = false };
            _music.ProcessMode = ProcessModeEnum.Always;
            AddChild(_music);
            _music.Play();
            _playback = _music.GetStreamPlayback() as AudioStreamGeneratorPlayback;

            // ── 音效 ──
            _sfx["place"] = BuildWav(Tone(0.07f, t => 720f - 3800f * t, t => 0.55f * MathF.Exp(-t * 40f)));
            _sfx["demolish"] = BuildWav(Mix(Noise(0.16f, 0.35f, 20f), Tone(0.16f, _ => 130f, t => 0.5f * MathF.Exp(-t * 16f))));
            _sfx["shot"] = BuildWav(Tone(0.06f, t => 1300f - 9000f * t, t => 0.5f * MathF.Exp(-t * 50f)));
            _sfx["error"] = BuildWav(Tone(0.18f, _ => 210f, t => 0.4f * MathF.Exp(-t * 14f) * (t < 0.09f ? 1f : 0f)));
            _sfx["research"] = BuildWav(Arp(new[] { 660f, 880f, 1320f }, 0.10f));
            _sfx["alert"] = BuildWav(Beeps(880f, 660f, 0.18f));
            _sfx["destroy"] = BuildWav(Noise(0.42f, 0.5f, 9f));
            _sfx["coin"] = BuildWav(Arp(new[] { 990f, 1320f }, 0.05f));
            _sfx["alienDie"] = BuildWav(Tone(0.18f, t => 500f - 1500f * t, t => 0.45f * MathF.Exp(-t * 16f)));
            _sfx["travel"] = BuildWav(Arp(new[] { 440f, 660f, 880f, 1320f }, 0.07f));

            for (int i = 0; i < 8; i++)
            {
                var p = new AudioStreamPlayer();
                AddChild(p);
                _pool.Add(p);
            }
        }

        public void PlaySfx(string name)
        {
            if (_sfx.Count == 0 || !_sfx.TryGetValue(name, out var wav)) return;
            var p = _pool[_poolIdx];
            _poolIdx = (_poolIdx + 1) % _pool.Count;
            p.Stream = wav;
            p.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(Settings2D.SfxVolume, 0.0001f, 1f));
            p.Play();
        }

        public override void _Process(double delta)
        {
            if (_music == null || _playback == null) return;

            _music.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(Settings2D.MusicVolume, 0.0001f, 1f));

            int frames = _playback.GetFramesAvailable();
            for (int i = 0; i < frames; i++)
            {
                float s = SampleMusic(_musicTime);
                _playback.PushFrame(new Vector2(s, s));
                _musicTime += 1f / RATE;
            }
        }

        // ── 音樂取樣 ────────────────────────────────────────────
        private static float SampleMusic(float t)
        {
            int ci = (int)(t / ChordLen) % Chords.Length;
            var chord = Chords[ci];
            float chordT = t % ChordLen;

            // 低音
            float bass = MathF.Sin(Tau * Freq(chord[0]) * t) * 0.11f;

            // 弦墊（漸入漸出）
            float pad = 0f;
            float sw = 0.5f + 0.5f * MathF.Sin(Tau * (chordT / ChordLen) * 2f - MathF.PI * 0.5f);
            for (int i = 1; i < chord.Length; i++)
            {
                float f = Freq(chord[i]);
                pad += (MathF.Sin(Tau * f * t) + 0.35f * MathF.Sin(Tau * f * 2f * t));
            }
            pad *= 0.024f * sw;

            // 琶音（pluck）
            int step = (int)(chordT / ArpStep);
            int note = chord[1 + step % (chord.Length - 1)];
            float at = chordT - step * ArpStep;
            float arp = MathF.Sin(Tau * Freq(note) * t) * MathF.Exp(-at * 7f) * 0.05f;

            // 呼吸 LFO
            float breath = 0.85f + 0.15f * MathF.Sin(Tau * 0.05f * t);
            return (bass + pad + arp) * breath;
        }

        private static float Freq(int midi) => 440f * MathF.Pow(2f, (midi - 69) / 12f);

        // ── 音效合成 ────────────────────────────────────────────
        private static float[] Tone(float dur, Func<float, float> freq, Func<float, float> amp)
        {
            int n = (int)(dur * RATE);
            var s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / RATE;
                phase += Tau * freq(t) / RATE;
                s[i] = MathF.Sin(phase) * amp(t);
            }
            return s;
        }

        private static float[] Noise(float dur, float vol, float decay)
        {
            int n = (int)(dur * RATE);
            var s = new float[n];
            var rng = new Random(42);
            for (int i = 0; i < n; i++)
                s[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * vol * MathF.Exp(-i / RATE * decay);
            return s;
        }

        private static float[] Arp(float[] freqs, float step)
        {
            var total = new float[(int)(freqs.Length * step * RATE)];
            for (int k = 0; k < freqs.Length; k++)
            {
                var seg = Tone(step, _ => freqs[k], t => 0.5f * MathF.Exp(-t * 22f));
                Array.Copy(seg, 0, total, (int)(k * step * RATE), seg.Length);
            }
            return total;
        }

        private static float[] Beeps(float f1, float f2, float dur)
        {
            var a = Tone(dur, _ => f1, t => 0.4f * MathF.Exp(-t * 8f));
            var b = Tone(dur, _ => f2, t => 0.4f * MathF.Exp(-t * 8f));
            var total = new float[a.Length + b.Length];
            Array.Copy(a, 0, total, 0, a.Length);
            Array.Copy(b, 0, total, a.Length, b.Length);
            return total;
        }

        private static float[] Mix(float[] a, float[] b)
        {
            int n = Math.Max(a.Length, b.Length);
            var s = new float[n];
            for (int i = 0; i < n; i++)
                s[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
            return s;
        }

        private static AudioStreamWav BuildWav(float[] samples)
        {
            var wav = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = (int)RATE,
                Stereo = false,
            };
            var data = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short v = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
                data[i * 2] = (byte)(v & 0xFF);
                data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
            wav.Data = data;
            return wav;
        }
    }
}
