using UnityEngine;

namespace NeuroSlop
{
    public enum MusicTrack { None, Menu, Level, Boss, Results }

    /// <summary>
    /// Procedural chip loop. With corruption the loop wobbles in pitch and stutters
    /// (re-plays a sixteenth) — "looped fragments" from the GDD's sound section.
    /// </summary>
    public class Music : MonoBehaviour
    {
        static Music instance;
        AudioSource source;
        MusicTrack current;
        float stepSeconds;
        float nextStutterCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static void Play(MusicTrack track)
        {
            if (instance == null)
            {
                var go = new GameObject("[Music]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Music>();
                instance.source = go.AddComponent<AudioSource>();
                instance.source.loop = true;
                instance.source.playOnAwake = false;
                instance.source.volume = 0.32f;
            }
            instance.Switch(track);
        }

        void Switch(MusicTrack track)
        {
            if (track == current) return;
            current = track;
            source.Stop();
            if (track == MusicTrack.None) return;
            source.clip = Compose(track, out stepSeconds);
            source.Play();
        }

        void Update()
        {
            if (source == null || !source.isPlaying) return;
            float c = CorruptionSystem.Intensity;
            source.pitch = (TimeControl.Paused ? 0.6f : 1f) + Mathf.Sin(Time.unscaledTime * 1.7f) * 0.035f * c;

            if (Time.unscaledTime < nextStutterCheck) return;
            nextStutterCheck = Time.unscaledTime + stepSeconds * 2f;
            if (Random.value < c * c * 0.35f)
            {
                int back = Mathf.RoundToInt(stepSeconds * Sfx.Rate);
                source.timeSamples = Mathf.Max(0, source.timeSamples - back);
            }
        }

        static AudioClip Compose(MusicTrack track, out float step)
        {
            float bpm = track switch { MusicTrack.Boss => 150f, MusicTrack.Level => 124f, _ => 92f };
            step = 60f / bpm / 4f;
            // bass roots per bar (semitones from A1) and arpeggio shapes
            int[] roots = track == MusicTrack.Boss ? new[] { 0, 0, 3, 1 } : track == MusicTrack.Level ? new[] { 0, 5, 3, 7 } : new[] { 0, 3, 5, 3 };
            int[] arp = track == MusicTrack.Boss ? new[] { 0, 12, 7, 12, 3, 15, 7, 10 } : new[] { 0, 7, 12, 15, 12, 7, 3, 7 };
            bool drums = track == MusicTrack.Level || track == MusicTrack.Boss;
            float leadVolume = track == MusicTrack.Menu || track == MusicTrack.Results ? 0.12f : 0.16f;

            int bars = roots.Length;
            float duration = bars * 16 * step;
            var noise = new System.Random(track.GetHashCode());
            float bassPhase = 0f, leadPhase = 0f;
            float stepLocal = step;

            return Sfx.Make("music_" + track, duration, t =>
            {
                int s = (int)(t / stepLocal);
                float inStep = t - s * stepLocal;
                int bar = (s / 16) % bars;
                int root = roots[bar];

                float bassFreq = 55f * Mathf.Pow(2f, root / 12f);
                bassPhase += bassFreq / Sfx.Rate;
                float bassGate = (s % 2 == 0) ? 1f : 0.55f;
                float bass = ((bassPhase % 1f) < 0.5f ? 1f : -1f) * 0.22f * bassGate * Mathf.Exp(-inStep * 6f);

                float leadFreq = 220f * Mathf.Pow(2f, (root + arp[s % arp.Length]) / 12f);
                leadPhase += leadFreq / Sfx.Rate;
                float lead = (1f - 4f * Mathf.Abs((leadPhase % 1f) - 0.5f)) * leadVolume * Mathf.Exp(-inStep * 10f);

                float drum = 0f;
                if (drums)
                {
                    if (s % 4 == 0) drum += Mathf.Sin(inStep * 2f * Mathf.PI * Mathf.Lerp(120f, 40f, inStep * 8f)) * 0.35f * Mathf.Exp(-inStep * 18f);
                    if (s % 4 == 2) drum += ((float)noise.NextDouble() * 2f - 1f) * 0.08f * Mathf.Exp(-inStep * 40f);
                    if (s % 8 == 4) drum += ((float)noise.NextDouble() * 2f - 1f) * 0.2f * Mathf.Exp(-inStep * 14f);
                }

                float mix = bass + lead + drum;
                return Mathf.Round(mix * 24f) / 24f; // bit-crush: cheap on purpose
            });
        }
    }
}
