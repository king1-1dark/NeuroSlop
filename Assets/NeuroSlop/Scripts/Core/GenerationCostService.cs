using UnityEngine;

namespace NeuroSlop
{
    /// <summary>Single entry point for billing generations; formulas live in GenerationCostConfig.</summary>
    public static class GenerationCostService
    {
        const string ResourcePath = "GenerationCostConfig";
        static GenerationCostConfig config;

        public static GenerationCostConfig Config
        {
            get
            {
                if (config == null)
                {
                    config = Resources.Load<GenerationCostConfig>(ResourcePath);
                    if (config == null) config = ScriptableObject.CreateInstance<GenerationCostConfig>();
                }
                return config;
            }
        }

        public static void Register(GenerationKind kind, int count = 1)
        {
            float wh = Config.EnergyFor(kind) * count;
            RunStats.AddGeneration(kind, count, wh, wh * Config.waterMlPerWh);
        }

        public static void TickIdle(float dt)
        {
            float wh = Config.idleWhPerSecond * dt;
            RunStats.AddConsumption(wh, wh * Config.waterMlPerWh);
        }

        public static string FormatEnergy(float wh) => wh >= 1000f ? $"{wh / 1000f:0.00} kWh" : $"{wh:0.0} Wh";
        public static string FormatWater(float ml) => ml >= 1000f ? $"{ml / 1000f:0.00} L" : $"{ml:0} ml";
        public static string FormatTime(float seconds) => $"{(int)(seconds / 60f):00}:{(int)(seconds % 60f):00}";
    }
}
