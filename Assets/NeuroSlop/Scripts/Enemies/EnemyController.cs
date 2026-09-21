using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// Small state machine shared by all regular enemies:
    /// Spawning → Idle → Chase → Windup (telegraph) → Attack → Recover/Retreat, plus Hurt and Dead.
    /// A two-token limit keeps the crowd from attacking all at once.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class EnemyController : MonoBehaviour
    {
        const int MaxSimultaneousAttackers = 2;
        public static readonly List<EnemyController> Active = new();
        static int attackTokens;

        [SerializeField] EnemyConfig config;
        [SerializeField] CharacterView view;
        [SerializeField] float spawnDuration = 0.7f;

        enum State { Spawning, Idle, Chase, Windup, Attack, Recover, Retreat, Hurt, Dead }

        Rigidbody2D body;
        Health health;
        State state = State.Spawning;
        float stateTimer;
        float nextAttackTime;
        float desiredVelocityX;
        int facing = -1;
        int side = 1;
        bool holdsToken;
        bool attackHit;

        public EnemyConfig Config => config;
        public Health Health => health;
        public bool IsDead => state == State.Dead;
        public event Action<EnemyController> Died;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
            attackTokens = 0;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            health.Configure(config.maxHealth);
            health.DisplayName = config.displayName;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnEnable() => Active.Add(this);

        void OnDisable()
        {
            Active.Remove(this);
            ReleaseToken();
        }

        void Start() => StartCoroutine(SpawnRoutine());

        IEnumerator SpawnRoutine()
        {
            state = State.Spawning;
            health.Invulnerable = true;
            GenerationCostService.Register(GenerationKind.Character);
            MetaMessages.PromptAt("prompt: " + config.prompt, transform.position + Vector3.up * 3f);
            Sfx.Play(SfxId.Generate, 0.7f);
            float t = 0f;
            while (t < spawnDuration)
            {
                t += Time.deltaTime;
                view.Dissolve = 1f - t / spawnDuration;
                view.Glitch = 0.8f * (1f - t / spawnDuration);
                yield return null;
            }
            view.Dissolve = 0f;
            view.Glitch = 0f;
            health.Invulnerable = false;
            nextAttackTime = Time.time + 0.4f;
            SetState(State.Idle, 0f);
        }

        void Update()
        {
            if (state == State.Dead || state == State.Spawning) return;

            var player = PlayerController.Instance;
            bool playerAlive = player != null && !player.IsDead;
            float dx = playerAlive ? player.transform.position.x - transform.position.x : 0f;
            float dy = playerAlive ? player.transform.position.y - transform.position.y : 0f;
            stateTimer -= Time.deltaTime;
            desiredVelocityX = 0f;

            switch (state)
            {
                case State.Idle:
                    if (playerAlive && Mathf.Abs(dx) < config.detectRange) SetState(State.Chase, 0f);
                    break;

                case State.Chase:
                    if (!playerAlive) { SetState(State.Idle, 0f); break; }
                    Face(dx);
                    if (Mathf.Abs(dx) <= config.attackRange && Mathf.Abs(dy) < 1.2f && Time.time >= nextAttackTime && TryTakeToken())
                    {
                        attackHit = false;
                        SetState(State.Windup, config.windup);
                        break;
                    }
                    float targetX = player.transform.position.x + side * Standoff();
                    float toTarget = targetX - transform.position.x;
                    if (Mathf.Abs(toTarget) > 0.15f) desiredVelocityX = Mathf.Sign(toTarget) * config.moveSpeed;
                    break;

                case State.Windup:
                    if (stateTimer <= 0f)
                    {
                        SetState(State.Attack, config.active);
                        if (config.lungeSpeed > 0f) Sfx.Play(SfxId.Swing, 1f, 1.3f);
                    }
                    break;

                case State.Attack:
                    desiredVelocityX = config.lungeSpeed * facing;
                    if (!attackHit) attackHit = DoHit();
                    if (stateTimer <= 0f)
                    {
                        ReleaseToken();
                        nextAttackTime = Time.time + config.cooldown;
                        if (config.retreatTime > 0f) SetState(State.Retreat, config.retreatTime);
                        else SetState(State.Recover, config.recovery);
                    }
                    break;

                case State.Recover:
                    if (stateTimer <= 0f) SetState(State.Chase, 0f);
                    break;

                case State.Retreat:
                    desiredVelocityX = -Mathf.Sign(dx == 0f ? facing : dx) * config.moveSpeed * 0.8f;
                    if (stateTimer <= 0f) SetState(State.Chase, 0f);
                    break;

                case State.Hurt:
                    if (stateTimer <= 0f) SetState(State.Chase, 0f);
                    break;
            }

            view.SetFacing(facing);
            view.SetMoving(Mathf.Abs(desiredVelocityX) > 0.1f);
            view.SetTelegraph(state == State.Windup);
            view.SetPose(state switch
            {
                State.Windup => CharacterView.Pose.Windup,
                State.Attack => CharacterView.Pose.Attack,
                _ => CharacterView.Pose.Idle
            });
        }

        void FixedUpdate()
        {
            var v = body.linearVelocity;
            if (state == State.Hurt || state == State.Dead)
                v.x = Mathf.MoveTowards(v.x, 0f, 25f * Time.fixedDeltaTime);
            else
                v.x = desiredVelocityX;
            body.linearVelocity = v;
        }

        /// <summary>Stand at attack distance; wait further back if an ally on our side is closer.</summary>
        float Standoff()
        {
            var player = PlayerController.Instance;
            float myDist = Mathf.Abs(player.transform.position.x - transform.position.x);
            int closerOnSide = 0, otherSide = 0;
            foreach (var e in Active)
            {
                if (e == this || e.IsDead || e.state == State.Spawning) continue;
                float ex = e.transform.position.x - player.transform.position.x;
                if (Mathf.Sign(ex) == side) { if (Mathf.Abs(ex) < myDist) closerOnSide++; }
                else otherSide++;
            }
            if (closerOnSide > 0 && otherSide == 0) { side = -side; return config.attackRange * 0.8f; }
            return config.attackRange * 0.8f + closerOnSide * 1.6f;
        }

        void Face(float dx)
        {
            if (Mathf.Abs(dx) > 0.05f) facing = dx > 0f ? 1 : -1;
        }

        bool DoHit()
        {
            var center = (Vector2)transform.position + new Vector2(config.hitOffset.x * facing, config.hitOffset.y);
            var info = new DamageInfo
            {
                Amount = config.damage,
                KnockbackX = config.knockback * facing,
                KnockbackY = 4f,
                Stun = 0.3f,
                Source = gameObject
            };
            return Hits.Box(center, config.hitSize, Team.Enemy, info) > 0;
        }

        void SetState(State next, float duration)
        {
            state = next;
            stateTimer = duration;
            if (next == State.Chase && PlayerController.Instance != null)
                side = transform.position.x >= PlayerController.Instance.transform.position.x ? 1 : -1;
        }

        bool TryTakeToken()
        {
            if (holdsToken) return true;
            if (attackTokens >= MaxSimultaneousAttackers) return false;
            attackTokens++;
            holdsToken = true;
            return true;
        }

        void ReleaseToken()
        {
            if (!holdsToken) return;
            holdsToken = false;
            attackTokens = Mathf.Max(0, attackTokens - 1);
        }

        void OnDamaged(DamageInfo info)
        {
            if (health.IsDead) return;
            ReleaseToken();
            view.Flash(Color.white);
            view.GlitchBurst(0.1f);
            float resist = 1f - config.knockbackResistance;
            body.linearVelocity = new Vector2(info.KnockbackX * resist, info.KnockbackY * resist);
            SetState(State.Hurt, info.Stun * (1f - config.stunResistance));
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 0.35f);
        }

        void OnDied()
        {
            ReleaseToken();
            state = State.Dead;
            RunStats.AddKill();
            Sfx.Play(SfxId.EnemyDie);
            view.SetTelegraph(false);
            view.Flash(Color.white);
            body.linearVelocity = new Vector2(Mathf.Sign(body.linearVelocity.x == 0f ? -facing : body.linearVelocity.x) * 3f, 6f);
            MetaMessages.PromptAt("DELETED: " + config.displayName, transform.position + Vector3.up * 2.5f);
            Died?.Invoke(this);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            yield return view.DeathRoutine(-facing, true);
            Destroy(gameObject);
        }

        /// <summary>Removes the enemy instantly (level reset / boss death), no rewards.</summary>
        public void Despawn()
        {
            ReleaseToken();
            state = State.Dead;
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            if (config == null) return;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + new Vector3(config.hitOffset.x * facing, config.hitOffset.y), config.hitSize);
        }
    }
}
