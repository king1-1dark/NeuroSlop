using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// One button, a 3-hit string: jab → jab → heavy (knockback). Hitbox data lives in the
    /// AttackStep array, not in the animation, so it can be tuned in the inspector.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [Serializable]
        public struct AttackStep
        {
            public int damage;
            public float windup;
            public float active;
            public float recovery;
            public Vector2 offset;
            public Vector2 size;
            public float knockback;
            public float stun;
            public bool heavy;
        }

        [SerializeField] AttackStep[] chain =
        {
            new AttackStep { damage = 8, windup = 0.05f, active = 0.08f, recovery = 0.14f, offset = new Vector2(0.95f, 1.55f), size = new Vector2(1.4f, 1.0f), knockback = 1.5f, stun = 0.35f },
            new AttackStep { damage = 8, windup = 0.05f, active = 0.08f, recovery = 0.14f, offset = new Vector2(0.95f, 1.55f), size = new Vector2(1.4f, 1.0f), knockback = 1.5f, stun = 0.35f },
            new AttackStep { damage = 16, windup = 0.1f, active = 0.1f, recovery = 0.3f, offset = new Vector2(1.05f, 1.5f), size = new Vector2(1.6f, 1.2f), knockback = 8f, stun = 0.7f, heavy = true },
        };
        [SerializeField] float attackBuffer = 0.25f;
        [SerializeField] float chainWindow = 0.35f;
        [SerializeField] float groundedMoveMultiplier = 0.1f;

        enum Phase { None, Windup, Active, Recovery }

        PlayerController controller;
        PlayerInputReader input;
        readonly HashSet<Health> hitThisSwing = new();
        Phase phase;
        float timer;
        int step = -1;
        float lastAttackEnd = float.NegativeInfinity;
        bool queued;

        public bool IsAttacking => phase != Phase.None;
        public bool ShowPunchPose => phase == Phase.Active || (phase == Phase.Recovery && timer > Current.recovery * 0.4f);
        AttackStep Current => chain[Mathf.Clamp(step, 0, chain.Length - 1)];

        public float MoveMultiplier(bool grounded) => IsAttacking && grounded ? groundedMoveMultiplier : 1f;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            input = GetComponent<PlayerInputReader>();
        }

        void Update()
        {
            if (controller.IsDead || controller.ControlLocked || controller.IsHurt || TimeControl.Paused)
            {
                if (!TimeControl.Paused) Cancel();
                return;
            }

            if (phase == Phase.None)
            {
                if (input.ConsumeAttack(attackBuffer))
                {
                    bool continues = Time.time - lastAttackEnd <= chainWindow && step < chain.Length - 1;
                    Begin(continues ? step + 1 : 0);
                }
                return;
            }

            if (input.ConsumeAttack(attackBuffer)) queued = true;

            timer -= Time.deltaTime;
            switch (phase)
            {
                case Phase.Windup:
                    if (timer <= 0f)
                    {
                        phase = Phase.Active;
                        timer = Current.active;
                        DoHit();
                    }
                    break;
                case Phase.Active:
                    DoHit();
                    if (timer <= 0f)
                    {
                        phase = Phase.Recovery;
                        timer = Current.recovery;
                    }
                    break;
                case Phase.Recovery:
                    if (queued && step < chain.Length - 1 && timer <= Current.recovery * 0.6f)
                        Begin(step + 1);
                    else if (timer <= 0f)
                    {
                        phase = Phase.None;
                        lastAttackEnd = Time.time;
                        if (step >= chain.Length - 1) step = -1;
                    }
                    break;
            }
        }

        void Begin(int index)
        {
            step = index;
            phase = Phase.Windup;
            timer = Current.windup;
            queued = false;
            hitThisSwing.Clear();
            Sfx.Play(SfxId.Swing, 1f, Current.heavy ? 0.7f : 1f + index * 0.1f);
        }

        void DoHit()
        {
            var s = Current;
            var center = (Vector2)transform.position + new Vector2(s.offset.x * controller.Facing, s.offset.y);
            var info = new DamageInfo
            {
                Amount = s.damage,
                KnockbackX = s.knockback * controller.Facing,
                KnockbackY = s.heavy ? 5f : 0f,
                Stun = s.stun,
                Heavy = s.heavy,
                Source = gameObject
            };
            Hits.Box(center, s.size, Team.Player, info, hitThisSwing);
        }

        public void Cancel()
        {
            phase = Phase.None;
            queued = false;
            step = -1;
        }

        void OnDrawGizmosSelected()
        {
            if (chain == null || chain.Length == 0) return;
            int facing = Application.isPlaying && controller != null ? controller.Facing : 1;
            Gizmos.color = Color.red;
            foreach (var s in chain)
                Gizmos.DrawWireCube(transform.position + new Vector3(s.offset.x * facing, s.offset.y), s.size);
        }
    }
}
