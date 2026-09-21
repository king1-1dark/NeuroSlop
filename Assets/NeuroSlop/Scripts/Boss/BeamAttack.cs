using System.Collections;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// "render(beam)": the danger band along the floor blinks, then a beam sweeps the arena.
    /// Counter: jump over it.
    /// </summary>
    public class BeamAttack : BossAttack
    {
        [SerializeField] SpriteRenderer warningBand;
        [SerializeField] SpriteRenderer beam;
        [SerializeField] float bottom = 0.05f;
        [SerializeField] float height = 1.05f;
        [SerializeField] float telegraph = 1.1f;
        [SerializeField] float duration = 0.4f;
        [SerializeField] int damage = 16;

        /// <summary>Seconds until the beam fires; negative when not telegraphing.</summary>
        public float TelegraphRemaining { get; private set; } = -1f;

        void Awake() => Cancel();

        public override IEnumerator Run(BossController boss, float speed)
        {
            float left = boss.ArenaMin;
            float right = boss.ArenaMax;
            var center = new Vector3((left + right) * 0.5f, bottom + height * 0.5f, -0.5f);
            var size = new Vector3(right - left, height, 1f);

            boss.SetFace(BossController.Face.Charge);
            Announce(consoleLine);
            warningBand.transform.position = center;
            warningBand.transform.localScale = size;
            warningBand.enabled = true;

            float t = telegraph / speed;
            while (t > 0f)
            {
                t -= Time.deltaTime;
                TelegraphRemaining = t;
                var c = warningBand.color;
                c.a = Mathf.Repeat(t * 8f, 1f) < 0.5f ? 0.45f : 0.15f;
                warningBand.color = c;
                yield return null;
            }
            warningBand.enabled = false;
            TelegraphRemaining = -1f;

            beam.transform.position = center;
            beam.enabled = true;
            Sfx.Play(SfxId.Beam);
            CameraRig.Shake(0.15f, duration);
            var info = new DamageInfo { Amount = damage, KnockbackX = -6f, KnockbackY = 6f, Stun = 0.3f, Source = gameObject };
            bool hit = false;
            float d = duration;
            while (d > 0f)
            {
                d -= Time.deltaTime;
                beam.transform.localScale = new Vector3(size.x, height * Random.Range(0.8f, 1.1f), 1f);
                if (!hit) hit = Hits.Box(center, new Vector2(size.x, height), Team.Enemy, info) > 0;
                yield return null;
            }
            beam.enabled = false;
        }

        public override void Cancel()
        {
            TelegraphRemaining = -1f;
            if (warningBand != null) warningBand.enabled = false;
            if (beam != null) beam.enabled = false;
        }
    }
}
