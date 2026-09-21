using System;
using System.Collections;
using UnityEngine;

namespace NeuroSlop
{
    [Serializable]
    public class BossPhase
    {
        public string name = "Phase";
        [Tooltip("Phase starts when boss health (0..1) drops to this value.")]
        [Range(0f, 1f)] public float enterAtHealth = 1f;
        public BossAttack[] attacks;
        public float idleTime = 1.3f;
        [Tooltip("Multiplies telegraph / attack speed.")]
        public float speed = 1f;
        public float corruptionOnEnter;
        public float bodyGlitch;
        [TextArea] public string[] entryLines;
    }

    /// <summary>
    /// The generation system as a boss. Phases are data: add a BossPhase with its own
    /// BossAttack components to extend the fight without touching this class.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class BossController : MonoBehaviour
    {
        public enum Face { Idle, Charge, Hurt }

        public static event Action<BossController> Activated;

        [SerializeField] string displayName = "GENERATOR v0.1";
        [SerializeField] int maxHealth = 260;
        [SerializeField] BossPhase[] phases;
        [SerializeField] CharacterView view;
        [SerializeField] SpriteRenderer faceRenderer;
        [SerializeField] Sprite faceIdle;
        [SerializeField] Sprite faceCharge;
        [SerializeField] Sprite faceHurt;
        [SerializeField] Transform muzzle;
        [SerializeField] float introDuration = 2.2f;

        Health health;
        Coroutine brain;
        BossAttack currentAttack;
        Face face;
        float hurtFaceUntil;
        int attackCursor;

        public Health Health => health;
        public string DisplayName => displayName;
        public int PhaseIndex { get; private set; }
        public BossPhase Phase => phases[PhaseIndex];
        public float ArenaMin { get; private set; }
        public float ArenaMax { get; private set; }
        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position + Vector3.up * 3f;
        public bool IsActive { get; private set; }
        public event Action Defeated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Activated = null;

        void Awake()
        {
            health = GetComponent<Health>();
            health.Configure(maxHealth);
            health.DisplayName = displayName;
            health.Invulnerable = true;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            view.Dissolve = 1f;
        }

        public void Activate(float arenaMin, float arenaMax)
        {
            ArenaMin = arenaMin;
            ArenaMax = arenaMax;
            if (IsActive || health.IsDead) return;
            IsActive = true;
            brain = StartCoroutine(Brain(true));
            Activated?.Invoke(this);
        }

        /// <summary>Player regenerated: stop the current attack, keep the damage dealt so far.</summary>
        public void ResetForRespawn()
        {
            if (health.IsDead) return;
            StopBrain();
            if (IsActive) brain = StartCoroutine(Brain(false));
        }

        void StopBrain()
        {
            if (brain != null) StopCoroutine(brain);
            brain = null;
            if (currentAttack != null) currentAttack.Cancel();
            currentAttack = null;
            foreach (var phase in phases)
                foreach (var attack in phase.attacks)
                    if (attack != null) attack.Cancel();
            SetFace(Face.Idle);
        }

        IEnumerator Brain(bool intro)
        {
            if (intro)
            {
                var texts = NarrativeTexts.Load();
                GenerationCostService.Register(GenerationKind.Scene);
                Sfx.Play(SfxId.Generate);
                MetaMessages.ShowHeadline(displayName, 2f);
                float t = 0f;
                int line = 0;
                while (t < introDuration)
                {
                    t += Time.deltaTime;
                    view.Dissolve = 1f - t / introDuration;
                    view.Glitch = 0.7f * (1f - t / introDuration);
                    if (line < texts.bossIntroLines.Length && t > line * 1.1f)
                        MetaMessages.Post(texts.bossIntroLines[line++]);
                    yield return null;
                }
                view.Dissolve = 0f;
                view.Glitch = 0f;
                EnterPhase(0);
            }
            else
            {
                yield return new WaitForSeconds(1.2f);
            }

            health.Invulnerable = false;
            while (!health.IsDead)
            {
                yield return new WaitForSeconds(Phase.idleTime / Phase.speed);
                CheckPhase();
                var attack = NextAttack();
                if (attack == null) continue;
                currentAttack = attack;
                GenerationCostService.Register(GenerationKind.Attack);
                yield return attack.Run(this, Phase.speed);
                currentAttack = null;
                SetFace(Face.Idle);
                CheckPhase();
            }
        }

        BossAttack NextAttack()
        {
            var attacks = Phase.attacks;
            for (int i = 0; i < attacks.Length; i++)
            {
                var candidate = attacks[attackCursor++ % attacks.Length];
                if (candidate != null && candidate.CanRun(this)) return candidate;
            }
            return null;
        }

        void CheckPhase()
        {
            for (int i = phases.Length - 1; i > PhaseIndex; i--)
            {
                if (health.Normalized <= phases[i].enterAtHealth)
                {
                    EnterPhase(i);
                    return;
                }
            }
        }

        void EnterPhase(int index)
        {
            PhaseIndex = index;
            attackCursor = 0;
            var phase = phases[index];
            view.Glitch = phase.bodyGlitch;
            if (phase.corruptionOnEnter > 0f) CorruptionSystem.Add(phase.corruptionOnEnter);
            if (index > 0)
            {
                CorruptionSystem.TriggerBurst(1f);
                Sfx.Play(SfxId.Glitch);
                CameraRig.Shake(0.35f, 0.5f);
                MetaMessages.ShowHeadline("PHASE " + (index + 1) + ": " + phase.name, 1.8f);
            }
            if (phase.entryLines != null)
                foreach (var line in phase.entryLines) MetaMessages.Post(line);
        }

        public void SetFace(Face value)
        {
            face = value;
            ApplyFace();
        }

        void ApplyFace()
        {
            if (faceRenderer == null) return;
            var shown = Time.time < hurtFaceUntil ? Face.Hurt : face;
            faceRenderer.sprite = shown switch { Face.Charge => faceCharge, Face.Hurt => faceHurt, _ => faceIdle };
        }

        void Update()
        {
            ApplyFace();
            if (faceRenderer != null)
                faceRenderer.enabled = view.Body.enabled && view.Dissolve < 0.5f;
        }

        void OnDamaged(DamageInfo info)
        {
            view.Flash(Color.white, 0.4f);
            hurtFaceUntil = Time.time + 0.25f;
            if (UnityEngine.Random.value < 0.15f) view.GlitchBurst(0.15f);
        }

        void OnDied()
        {
            StopBrain();
            IsActive = false;
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            var texts = NarrativeTexts.Load();
            TimeControl.SlowMotion(0.3f, 1.2f);
            Sfx.Play(SfxId.BossDie);
            CorruptionSystem.TriggerBurst(1f);
            MetaMessages.ShowHeadline(texts.victoryLines.Length > 0 ? texts.victoryLines[0] : "GENERATION FAILED", 3f);
            for (int i = 1; i < texts.victoryLines.Length; i++) MetaMessages.Post(texts.victoryLines[i]);
            float t = 0f;
            while (t < 2.5f)
            {
                t += Time.deltaTime;
                view.Glitch = 1f;
                view.Dissolve = Mathf.Clamp01((t - 0.8f) / 1.7f);
                if (UnityEngine.Random.value < 0.3f) CameraRig.Shake(0.3f, 0.1f);
                yield return null;
            }
            if (faceRenderer != null) faceRenderer.enabled = false;
            view.Body.enabled = false;
            Defeated?.Invoke();
        }
    }
}
