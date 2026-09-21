using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// Slowly breaks a UI panel as corruption rises: permanent tilt / misalignment from tier 2,
    /// plus short position jumps. At tier 0 the panel is untouched.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIDegrader : MonoBehaviour
    {
        [SerializeField] float maxTilt = 4f;
        [SerializeField] float maxDrift = 18f;
        [SerializeField] float jumpPixels = 14f;

        RectTransform rt;
        Vector2 basePosition;
        float tiltSign;
        float jumpUntil;
        Vector2 jumpOffset;

        void Awake()
        {
            rt = (RectTransform)transform;
            basePosition = rt.anchoredPosition;
            tiltSign = Random.value < 0.5f ? -1f : 1f;
        }

        void LateUpdate()
        {
            float c = CorruptionSystem.Intensity;
            float broken = Mathf.Clamp01((CorruptionSystem.Level - 0.45f) / 0.55f);

            if (Time.unscaledTime > jumpUntil && Random.value < c * c * Time.unscaledDeltaTime * 2f)
            {
                jumpUntil = Time.unscaledTime + Random.Range(0.05f, 0.15f);
                jumpOffset = Random.insideUnitCircle * jumpPixels * (0.5f + c);
            }
            var offset = Time.unscaledTime < jumpUntil ? jumpOffset : Vector2.zero;

            rt.anchoredPosition = basePosition + new Vector2(broken * maxDrift * tiltSign, -broken * maxDrift * 0.5f) + offset;
            rt.localRotation = Quaternion.Euler(0f, 0f, broken * maxTilt * tiltSign);
        }
    }
}
