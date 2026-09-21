using UnityEngine;

namespace NeuroSlop
{
    /// <summary>Tuning data of one enemy type. Behaviour differences come from these numbers.</summary>
    [CreateAssetMenu(menuName = "NeuroSlop/Enemy Config", fileName = "EnemyConfig")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "THUG_BOT.png";
        [TextArea] public string prompt = "robot thug, 16-bit";

        [Header("Stats")]
        public int maxHealth = 36;
        public float moveSpeed = 2.2f;
        public float detectRange = 14f;

        [Header("Attack")]
        public float attackRange = 1.5f;
        public float windup = 0.55f;
        public float active = 0.12f;
        public float recovery = 0.6f;
        public float cooldown = 1.1f;
        public int damage = 12;
        public Vector2 hitOffset = new(0.9f, 1.4f);
        public Vector2 hitSize = new(1.3f, 1.0f);
        public float knockback = 5f;

        [Header("Personality")]
        [Tooltip(">0: dashes forward during the attack (closes the gap from further away).")]
        public float lungeSpeed;
        [Tooltip(">0: backs off for this long after attacking — hit and run.")]
        public float retreatTime;
        [Range(0f, 1f)] public float stunResistance;
        [Range(0f, 1f)] public float knockbackResistance;
    }
}
