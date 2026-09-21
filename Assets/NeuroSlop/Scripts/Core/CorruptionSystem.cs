using System;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// GameCorruption: one 0..1 value describing how broken the game currently is.
    /// Gameplay pushes it (level base, encounters, deaths, boss phases); effects only read
    /// Level / Tier / Intensity. Tier 0 ≈ "Level 1", tier 3 ≈ final boss territory.
    /// </summary>
    public class CorruptionSystem : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float baseLevel = 0.1f;
        [SerializeField] float perDeath = 0.02f;
        [SerializeField] float spikeDecayPerSecond = 1.5f;

        public static float Level { get; private set; }
        public static float Spike { get; private set; }
        /// <summary>What visual effects should use: level plus the current burst.</summary>
        public static float Intensity => Mathf.Clamp01(Level + Spike * 0.6f);
        public static int Tier => Level < 0.25f ? 0 : Level < 0.5f ? 1 : Level < 0.75f ? 2 : 3;

        /// <summary>Sudden glitch moment (strength 0..1): screen tear, sound stutter, etc.</summary>
        public static event Action<float> Burst;

        static CorruptionSystem instance;
        static float bonus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Level = 0f;
            Spike = 0f;
            bonus = 0f;
            Burst = null;
            instance = null;
        }

        void Awake()
        {
            instance = this;
            bonus = 0f;
            Spike = 0f;
            Recalculate();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            Spike = Mathf.MoveTowards(Spike, 0f, Time.unscaledDeltaTime * spikeDecayPerSecond);
            Recalculate();
        }

        void Recalculate() => Level = Mathf.Clamp01(baseLevel + bonus + RunStats.Deaths * perDeath);

        public static void Add(float amount)
        {
            bonus += amount;
            if (instance) instance.Recalculate();
        }

        public static void TriggerBurst(float strength)
        {
            Spike = Mathf.Max(Spike, Mathf.Clamp01(strength));
            Burst?.Invoke(strength);
        }
    }
}
