using UnityEngine;
using UnityEngine.UI;

namespace NeuroSlop
{
    /// <summary>
    /// Screen tearing: coloured horizontal bars flashing over the whole screen. Frequency grows
    /// with corruption; CorruptionSystem.Burst forces a strong tear.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ScreenGlitchOverlay : MonoBehaviour
    {
        [SerializeField] int barCount = 10;

        static readonly Color[] Palette =
        {
            new(1f, 0f, 1f, 0.55f), new(0f, 1f, 1f, 0.5f), new(0f, 0f, 0f, 0.8f),
            new(1f, 1f, 1f, 0.35f), new(0.2f, 1f, 0.3f, 0.45f)
        };

        Image[] bars;
        float[] barUntil;

        void Awake()
        {
            bars = new Image[barCount];
            barUntil = new float[barCount];
            for (int i = 0; i < barCount; i++)
            {
                var go = new GameObject("Tear" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.enabled = false;
                bars[i] = img;
            }
        }

        void OnEnable() => CorruptionSystem.Burst += OnBurst;
        void OnDisable() => CorruptionSystem.Burst -= OnBurst;

        void OnBurst(float strength) => Tear(Mathf.RoundToInt(2 + strength * 6), 0.08f + strength * 0.15f);

        void Update()
        {
            float c = CorruptionSystem.Intensity;
            if (Random.value < c * c * c * Time.unscaledDeltaTime * 6f) Tear(Random.Range(1, 4), Random.Range(0.04f, 0.12f));

            float now = Time.unscaledTime;
            for (int i = 0; i < bars.Length; i++)
                if (bars[i].enabled && now > barUntil[i]) bars[i].enabled = false;
        }

        void Tear(int count, float duration)
        {
            var size = ((RectTransform)transform).rect.size;
            for (int n = 0, i = Random.Range(0, bars.Length); n < count; n++, i = (i + 1) % bars.Length)
            {
                var img = bars[i];
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size.x * Random.Range(0.3f, 1.1f), Random.Range(4f, 40f));
                rt.anchoredPosition = new Vector2(Random.Range(-0.3f, 0.3f) * size.x, Random.Range(-0.5f, 0.5f) * size.y);
                img.color = Palette[Random.Range(0, Palette.Length)];
                img.enabled = true;
                barUntil[i] = Time.unscaledTime + duration * Random.Range(0.6f, 1.4f);
            }
        }
    }
}
