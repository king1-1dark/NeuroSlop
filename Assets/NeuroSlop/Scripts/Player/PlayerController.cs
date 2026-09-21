using System;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>Movement, jump, damage reaction and death of the developer.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }
        public static event Action<PlayerController> PlayerDied;

        [Header("Movement")]
        [SerializeField] float moveSpeed = 5.4f;
        [SerializeField] float jumpVelocity = 13.5f;
        [SerializeField] float jumpCutMultiplier = 0.5f;
        [SerializeField] float coyoteTime = 0.1f;
        [SerializeField] float jumpBuffer = 0.14f;
        [SerializeField] Vector2 groundCheckSize = new(0.6f, 0.12f);

        [Header("Damage reaction")]
        [SerializeField] float hurtTime = 0.3f;
        [SerializeField] float invulnerabilityTime = 1.1f;
        [SerializeField] float respawnInvulnerability = 2f;

        [Header("Links")]
        [SerializeField] CharacterView view;

        Rigidbody2D body;
        Health health;
        PlayerInputReader input;
        PlayerCombat combat;
        float lastGroundedTime = float.NegativeInfinity;
        float hurtUntil;
        float invulnerableUntil;
        bool jumpCut;

        public Health Health => health;
        public CharacterView View => view;
        public bool Grounded { get; private set; }
        public int Facing { get; private set; } = 1;
        public bool IsDead => health.IsDead;
        public bool IsHurt => Time.time < hurtUntil;
        public bool ControlLocked { get; set; }
        public Vector2 Velocity => body.linearVelocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            PlayerDied = null;
        }

        void Awake()
        {
            Instance = this;
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            input = GetComponent<PlayerInputReader>();
            combat = GetComponent<PlayerCombat>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            CheckGround();
            bool blinking = Time.time < invulnerableUntil;
            health.Invulnerable = blinking || ControlLocked;
            if (view != null) view.Blink = blinking && !IsDead;
            if (IsDead) return;

            bool canAct = !ControlLocked && !IsHurt && !TimeControl.Paused;
            bool canJump = Grounded || Time.time - lastGroundedTime <= coyoteTime;
            if (canAct && canJump && (combat == null || !combat.IsAttacking) && input.ConsumeJump(jumpBuffer))
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, jumpVelocity);
                lastGroundedTime = float.NegativeInfinity;
                Grounded = false;
                jumpCut = false;
                Sfx.Play(SfxId.Jump);
            }

            // Variable jump height: releasing the button early cuts the rise.
            if (!jumpCut && !input.JumpHeld && body.linearVelocity.y > 0f && !Grounded)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y * jumpCutMultiplier);
                jumpCut = true;
            }

            if (canAct && input.MoveX != 0f && (combat == null || !combat.IsAttacking))
                Facing = input.MoveX > 0f ? 1 : -1;

            if (view != null)
            {
                view.SetFacing(Facing);
                view.SetMoving(Grounded && Mathf.Abs(body.linearVelocity.x) > 0.1f);
                view.SetPose(combat != null && combat.ShowPunchPose ? CharacterView.Pose.Attack : CharacterView.Pose.Idle);
            }
        }

        void FixedUpdate()
        {
            var v = body.linearVelocity;
            if (IsDead || IsHurt)
            {
                v.x = Mathf.MoveTowards(v.x, 0f, (Grounded ? 30f : 8f) * Time.fixedDeltaTime);
            }
            else
            {
                float multiplier = combat != null ? combat.MoveMultiplier(Grounded) : 1f;
                float target = ControlLocked ? 0f : input.MoveX * moveSpeed * multiplier;
                v.x = target;
            }
            body.linearVelocity = v;
        }

        void CheckGround()
        {
            bool wasGrounded = Grounded;
            Grounded = body.linearVelocity.y <= 0.05f &&
                       Physics2D.OverlapBox((Vector2)transform.position, groundCheckSize, 0f, Layers.GroundMask) != null;
            if (Grounded)
            {
                lastGroundedTime = Time.time;
                jumpCut = true;
            }
            if (Grounded && !wasGrounded && view != null) view.Flash(Color.white, 0.15f);
        }

        void OnDamaged(DamageInfo info)
        {
            if (health.IsDead) return;
            hurtUntil = Time.time + hurtTime;
            invulnerableUntil = Time.time + invulnerabilityTime;
            body.linearVelocity = new Vector2(info.KnockbackX, Mathf.Max(3f, info.KnockbackY));
            if (view != null) view.Flash(new Color(1f, 0.2f, 0.2f));
            combat?.Cancel();
        }

        void OnDied()
        {
            combat?.Cancel();
            body.linearVelocity = new Vector2(-Facing * 4f, 7f);
            Sfx.Play(SfxId.PlayerDie);
            CorruptionSystem.TriggerBurst(1f);
            if (view != null)
            {
                view.Blink = false;
                StartCoroutine(view.DeathRoutine(-Facing, false));
            }
            PlayerDied?.Invoke(this);
        }

        public void Revive(Vector2 position)
        {
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            health.RestoreFull();
            hurtUntil = 0f;
            invulnerableUntil = Time.time + respawnInvulnerability;
            input.ClearBuffers();
            if (view != null)
            {
                StopAllCoroutines();
                view.ResetVisual();
                view.GlitchBurst(0.6f);
            }
            Sfx.Play(SfxId.Generate);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, groundCheckSize);
        }
    }
}
