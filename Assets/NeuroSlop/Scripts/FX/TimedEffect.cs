using UnityEngine;

namespace NeuroSlop
{
    /// <summary>Short-lived sprite effect (hit spark, dust): pops, spins, fades, self-destructs.</summary>
    public class TimedEffect : MonoBehaviour
    {
        [SerializeField] float lifetime = 0.18f;
        [SerializeField] float startScale = 0.6f;
        [SerializeField] float endScale = 1.3f;
        [SerializeField] float spin = 360f;

        SpriteRenderer sr;
        float age;
        float scaleMultiplier = 1f;

        public void SetScale(float multiplier) => scaleMultiplier = multiplier;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            transform.Rotate(0f, 0f, Random.Range(0f, 360f));
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float k = age / lifetime;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, k) * scaleMultiplier;
            transform.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
            if (sr != null)
            {
                var c = sr.color;
                c.a = 1f - k * k;
                sr.color = c;
            }
        }
    }
}
