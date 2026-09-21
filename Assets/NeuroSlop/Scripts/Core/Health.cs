using System;
using UnityEngine;

namespace NeuroSlop
{
    public enum Team { Player, Enemy }

    public struct DamageInfo
    {
        public int Amount;
        public float KnockbackX;   // signed horizontal knockback speed
        public float KnockbackY;
        public float Stun;         // seconds the receiver is stunned
        public bool Heavy;
        public Vector2 Point;
        public GameObject Source;
    }

    /// <summary>
    /// Single source of truth for hit points. Controllers subscribe to its events to react,
    /// UI reads it — nothing here knows about either.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] Team team = Team.Enemy;
        [SerializeField] int maxHealth = 100;
        [SerializeField] string displayName = "";

        public Team Team => team;
        public int Max => maxHealth;
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public float Normalized => maxHealth > 0 ? (float)Current / maxHealth : 0f;
        public bool Invulnerable { get; set; }
        public string DisplayName { get => displayName; set => displayName = value; }

        public event Action<DamageInfo> Damaged;
        public event Action Died;
        public event Action Changed;

        void Awake() => Current = maxHealth;

        public void Configure(int max)
        {
            maxHealth = Mathf.Max(1, max);
            Current = maxHealth;
            Changed?.Invoke();
        }

        public bool TakeDamage(DamageInfo info)
        {
            if (IsDead || Invulnerable || info.Amount <= 0) return false;
            Current = Mathf.Max(0, Current - info.Amount);
            Changed?.Invoke();
            Damaged?.Invoke(info);
            if (Current == 0) Died?.Invoke();
            return true;
        }

        public void RestoreFull()
        {
            Current = maxHealth;
            Changed?.Invoke();
        }
    }
}
