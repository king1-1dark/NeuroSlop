using System.Collections;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>A "missing texture" block dropped by the boss.</summary>
    public class FallingAsset : MonoBehaviour
    {
        [SerializeField] float fallSpeed = 17f;
        [SerializeField] float groundY = 0f;
        [SerializeField] Vector2 hitSize = new(1.4f, 1.4f);
        [SerializeField] int damage = 14;
        [SerializeField] CharacterView view;

        bool hit;
        public bool Landed { get; private set; }

        void Update()
        {
            if (Landed) return;
            var p = transform.position;
            p.y = Mathf.Max(groundY, p.y - fallSpeed * Time.deltaTime);
            transform.position = p;
            TryHit();
            if (p.y <= groundY) StartCoroutine(Land());
        }

        void TryHit()
        {
            if (hit) return;
            var info = new DamageInfo { Amount = damage, KnockbackX = Random.value < 0.5f ? -5f : 5f, KnockbackY = 6f, Stun = 0.3f, Source = gameObject };
            hit = Hits.Box((Vector2)transform.position + Vector2.up * hitSize.y * 0.5f, hitSize, Team.Enemy, info) > 0;
        }

        IEnumerator Land()
        {
            Landed = true;
            Sfx.Play(SfxId.Impact, 0.8f);
            CameraRig.Shake(0.2f, 0.15f);
            TryHit();
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                if (view != null)
                {
                    view.Glitch = 0.5f;
                    view.Dissolve = Mathf.Clamp01((t - 0.25f) / 0.35f);
                }
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
