using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// Conditional ("plausible, not scientific") cost model. Change numbers here or in the
    /// asset — UI and gameplay only call GenerationCostService.Register.
    /// </summary>
    [CreateAssetMenu(menuName = "NeuroSlop/Generation Cost Config", fileName = "GenerationCostConfig")]
    public class GenerationCostConfig : ScriptableObject
    {
        [Header("Energy per generation, Wh")]
        public float textWh = 0.3f;
        public float characterWh = 2.9f;
        public float attackWh = 0.6f;
        public float sceneWh = 8f;
        public float regenerationWh = 14f;

        [Header("Background inference while the game is running, Wh per second")]
        public float idleWhPerSecond = 0.05f;

        [Header("Water (cooling + power generation), ml per Wh")]
        public float waterMlPerWh = 1.9f;

        [Header("Comparisons shown on the results screen")]
        public float phoneChargeWh = 15f;
        public float glassOfWaterMl = 250f;

        public float EnergyFor(GenerationKind kind) => kind switch
        {
            GenerationKind.Text => textWh,
            GenerationKind.Character => characterWh,
            GenerationKind.Attack => attackWh,
            GenerationKind.Scene => sceneWh,
            GenerationKind.Regeneration => regenerationWh,
            _ => textWh
        };
    }
}
