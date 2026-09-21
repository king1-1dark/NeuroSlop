using System;
using UnityEngine;

namespace NeuroSlop
{
    public enum GenerationKind { Text, Character, Attack, Scene, Regeneration }

    /// <summary>
    /// Centralised numbers of the current run. Lives across scenes (static) so the results
    /// screen can read what the level produced. Only GenerationCostService writes resources.
    /// </summary>
    public static class RunStats
    {
        public static int Generations { get; private set; }
        public static float EnergyWh { get; private set; }
        public static float WaterMl { get; private set; }
        public static float TimeSeconds { get; private set; }
        public static int Deaths { get; private set; }
        public static int Kills { get; private set; }
        public static bool Completed { get; set; }

        /// <summary>kind, count, energy Wh of this generation.</summary>
        public static event Action<GenerationKind, int, float> Generated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Reset();
            Generated = null;
        }

        public static void Reset()
        {
            Generations = 0;
            EnergyWh = 0f;
            WaterMl = 0f;
            TimeSeconds = 0f;
            Deaths = 0;
            Kills = 0;
            Completed = false;
        }

        internal static void AddGeneration(GenerationKind kind, int count, float wh, float ml)
        {
            Generations += count;
            EnergyWh += wh;
            WaterMl += ml;
            Generated?.Invoke(kind, count, wh);
        }

        internal static void AddConsumption(float wh, float ml)
        {
            EnergyWh += wh;
            WaterMl += ml;
        }

        public static void AddTime(float dt) => TimeSeconds += dt;
        public static void AddKill() => Kills++;
        public static void AddDeath() => Deaths++;
    }
}
