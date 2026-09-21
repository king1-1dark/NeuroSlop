using System.Collections;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// Code-driven "animation" for characters that only have a couple of generated frames:
    /// pose sprites + bob + flash + corruption glitches. Lives on the visual child so that
    /// flipping never touches physics or hitboxes.
    /// </summary>
    public class CharacterView : MonoBehaviour
    {
        public enum Pose { Idle, Windup, Attack }

        [SerializeField] SpriteRenderer body;
        [SerializeField] Sprite idle;
        [SerializeField] Sprite windup;
        [SerializeField] Sprite attack;
        [SerializeField] float bobPixels = 1f;
        [SerializeField] float pixelsPerUnit = 32f;
        [SerializeField] float glitchSensitivity = 1f;
        [SerializeField] float baseGlitch;

        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly int GlitchId = Shader.PropertyToID("_Glitch");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int SeedId = Shader.PropertyToID("_Seed");

        MaterialPropertyBlock block;
        Pose pose;
        bool moving;
        bool telegraph;
        float flash;
        Color flashColor = Color.white;
        float glitchBurstUntil;
        float seed;

        public SpriteRenderer Body => body;
        public float Glitch { get; set; }
        public float Dissolve { get; set; }
        public bool Blink { get; set; }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            seed = Random.Range(0f, 100f);
            if (body == null) body = GetComponent<SpriteRenderer>();
        }

        public void SetFacing(int facing)
        {
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (facing < 0 ? -1f : 1f);
            transform.localScale = s;
        }

        public void SetPose(Pose p) => pose = p;
        public void SetMoving(bool value) => moving = value;
        public void SetTelegraph(bool value) => telegraph = value;

        public void Flash(Color color, float amount = 1f)
        {
            flashColor = color;
            flash = Mathf.Max(flash, amount);
        }

        public void GlitchBurst(float seconds) => glitchBurstUntil = Mathf.Max(glitchBurstUntil, Time.time + seconds);

        public void SetSprites(Sprite idleSprite, Sprite windupSprite, Sprite attackSprite)
        {
            idle = idleSprite;
            windup = windupSprite;
            attack = attackSprite;
        }

        public IEnumerator DeathRoutine(int facing, bool dissolve)
        {
            float t = 0f;
            float startAngle = transform.localEulerAngles.z;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startAngle, 80f * facing, t / 0.35f));
                yield return null;
            }
            if (!dissolve) yield break;
            t = 0f;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                Dissolve = t / 0.7f;
                Glitch = 0.6f;
                yield return null;
            }
        }

        public void ResetVisual()
        {
            transform.localRotation = Quaternion.identity;
            Dissolve = 0f;
            Glitch = 0f;
            Blink = false;
            flash = 0f;
            body.enabled = true;
        }

        void LateUpdate()
        {
            if (body == null) return;

            body.sprite = pose switch
            {
                Pose.Windup when windup != null => windup,
                Pose.Attack when attack != null => attack,
                _ => idle
            };

            // Jerky 2-frame bob instead of a walk cycle (there is no walk cycle — nobody generated one).
            float bob = moving && (Mathf.Repeat(Time.time * 7f, 1f) < 0.5f) ? bobPixels / pixelsPerUnit : 0f;
            body.transform.localPosition = new Vector3(0f, bob, 0f);

            body.enabled = !Blink || Mathf.Repeat(Time.time * 16f, 1f) < 0.5f;

            // Random glitch bursts scale with corruption.
            float c = CorruptionSystem.Intensity * glitchSensitivity;
            if (Random.value < c * c * Time.deltaTime * 1.5f) GlitchBurst(Random.Range(0.06f, 0.2f));
            float glitch = Mathf.Max(Mathf.Max(Glitch, baseGlitch), Time.time < glitchBurstUntil ? 0.35f + c * 0.5f : 0f);

            float telegraphFlash = telegraph ? (Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? 0.65f : 0.1f) : 0f;
            float f = Mathf.Max(flash, telegraphFlash);
            Color fc = flash >= telegraphFlash ? flashColor : new Color(1f, 0.25f, 0.2f);
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 8f);

            body.GetPropertyBlock(block);
            block.SetFloat(FlashId, f);
            block.SetColor(FlashColorId, fc);
            block.SetFloat(GlitchId, glitch);
            block.SetFloat(DissolveId, Dissolve);
            block.SetFloat(SeedId, seed);
            body.SetPropertyBlock(block);
        }
    }
}
