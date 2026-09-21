using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroSlop
{
    public enum SfxId { Jump, Swing, Hit, HitHeavy, Hurt, EnemyDie, Telegraph, Beam, Impact, Generate, Glitch, UiMove, UiConfirm, PlayerDie, BossDie }

    /// <summary>
    /// Lo-fi procedural sound: every clip is synthesised at 22 kHz on first use — no audio assets.
    /// Corruption detunes playback.
    /// </summary>
    public static class Sfx
    {
        public const int Rate = 22050;
        const int Voices = 12;

        static AudioSource[] sources;
        static int next;
        static readonly Dictionary<SfxId, AudioClip> clips = new();
        static readonly System.Random rng = new(1337);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sources = null;
            next = 0;
            clips.Clear();
        }

        public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            if (!Ensure()) return;
            if (!clips.TryGetValue(id, out var clip)) clips[id] = clip = Build(id);
            var source = sources[next++ % Voices];
            float wobble = 1f + UnityEngine.Random.Range(-0.04f, 0.04f) + UnityEngine.Random.Range(-0.25f, 0.25f) * CorruptionSystem.Intensity * CorruptionSystem.Intensity;
            source.pitch = pitch * wobble;
            source.volume = volume * 0.55f;
            source.clip = clip;
            source.Play();
        }

        static bool Ensure()
        {
            if (sources != null && sources[0] != null) return true;
            var go = new GameObject("[Sfx]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = go.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
            return true;
        }

        // ---- synthesis ------------------------------------------------------------------

        public enum Wave { Square, Saw, Triangle, Noise }

        static float Osc(Wave w, float phase)
        {
            float p = phase - Mathf.Floor(phase);
            return w switch
            {
                Wave.Square => p < 0.5f ? 1f : -1f,
                Wave.Saw => p * 2f - 1f,
                Wave.Triangle => 1f - 4f * Mathf.Abs(p - 0.5f),
                _ => (float)rng.NextDouble() * 2f - 1f
            };
        }

        public static AudioClip Make(string name, float duration, Func<float, float> sample)
        {
            int n = Mathf.Max(1, (int)(duration * Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Sweep(string name, float duration, float f0, float f1, Wave wave, float noise, float volume, float crush = 0f)
        {
            float phase = 0f;
            return Make(name, duration, t =>
            {
                float k = t / duration;
                phase += Mathf.Lerp(f0, f1, k) / Rate;
                float s = Osc(wave, phase);
                if (noise > 0f) s = Mathf.Lerp(s, Osc(Wave.Noise, 0f), noise);
                if (crush > 0f) s = Mathf.Round(s * crush) / crush;
                float env = (1f - k) * (1f - k) * Mathf.Min(1f, t * 300f);
                return s * env * volume;
            });
        }

        static AudioClip Arp(string name, float duration, float[] notes, float step, Wave wave, float volume, bool random)
        {
            float phase = 0f;
            return Make(name, duration, t =>
            {
                int i = (int)(t / step);
                float f = random ? notes[(i * 7 + 3) % notes.Length] : notes[Mathf.Min(i, notes.Length - 1)];
                phase += f / Rate;
                float k = t / duration;
                return Osc(wave, phase) * volume * (1f - k) * Mathf.Min(1f, t * 300f);
            });
        }

        static AudioClip Build(SfxId id) => id switch
        {
            SfxId.Jump => Sweep("jump", 0.14f, 220f, 560f, Wave.Square, 0f, 0.35f),
            SfxId.Swing => Sweep("swing", 0.09f, 900f, 300f, Wave.Noise, 1f, 0.35f),
            SfxId.Hit => Sweep("hit", 0.12f, 180f, 50f, Wave.Square, 0.5f, 0.7f, 6f),
            SfxId.HitHeavy => Sweep("hit_heavy", 0.24f, 140f, 35f, Wave.Square, 0.6f, 0.85f, 4f),
            SfxId.Hurt => Sweep("hurt", 0.28f, 480f, 110f, Wave.Saw, 0.15f, 0.55f),
            SfxId.EnemyDie => Arp("enemy_die", 0.45f, new[] { 880f, 220f, 660f, 110f, 990f, 330f }, 0.03f, Wave.Square, 0.35f, true),
            SfxId.Telegraph => Make("telegraph", 0.36f, t => Osc(Wave.Square, t * 1320f) * (((int)(t / 0.06f)) % 2 == 0 ? 0.25f : 0f)),
            SfxId.Beam => Sweep("beam", 0.6f, 60f, 90f, Wave.Saw, 0.45f, 0.6f, 5f),
            SfxId.Impact => Sweep("impact", 0.32f, 90f, 30f, Wave.Noise, 0.85f, 0.8f, 3f),
            SfxId.Generate => Arp("generate", 0.5f, new[] { 262f, 330f, 392f, 523f, 659f, 784f, 1046f }, 0.07f, Wave.Triangle, 0.35f, false),
            SfxId.Glitch => Arp("glitch", 0.22f, new[] { 1200f, 80f, 3000f, 150f, 600f }, 0.018f, Wave.Noise, 0.3f, true),
            SfxId.UiMove => Sweep("ui_move", 0.05f, 660f, 660f, Wave.Square, 0f, 0.25f),
            SfxId.UiConfirm => Sweep("ui_confirm", 0.14f, 660f, 1320f, Wave.Square, 0f, 0.3f),
            SfxId.PlayerDie => Arp("player_die", 0.9f, new[] { 523f, 466f, 392f, 330f, 262f, 196f, 131f }, 0.12f, Wave.Square, 0.4f, false),
            SfxId.BossDie => Sweep("boss_die", 1.8f, 400f, 20f, Wave.Noise, 0.7f, 0.9f, 3f),
            _ => Sweep("blip", 0.05f, 440f, 440f, Wave.Square, 0f, 0.2f)
        };
    }
}
