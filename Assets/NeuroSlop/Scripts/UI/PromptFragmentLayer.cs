using UnityEngine;
using UnityEngine.UI;

namespace NeuroSlop
{
    /// <summary>
    /// Shows short prompt fragments anchored to world positions (spawned enemies, deleted assets,
    /// ambient noise). They drift up and fade. Screen-space so they stay readable.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PromptFragmentLayer : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int poolSize = 10;
        [SerializeField] float lifetime = 2.6f;
        [SerializeField] int fontSize = 22;

        Text[] pool;
        float[] age;
        Vector3[] anchor;
        int next;
        Camera cam;
        RectTransform rect;

        void Awake()
        {
            rect = (RectTransform)transform;
            pool = new Text[poolSize];
            age = new float[poolSize];
            anchor = new Vector3[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Fragment" + i, typeof(RectTransform), typeof(Text), typeof(Outline));
                go.transform.SetParent(transform, false);
                var t = go.GetComponent<Text>();
                t.font = font;
                t.fontSize = fontSize;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.enabled = false;
                go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
                pool[i] = t;
                age[i] = lifetime;
            }
        }

        void OnEnable() => MetaMessages.WorldPromptPosted += Show;
        void OnDisable() => MetaMessages.WorldPromptPosted -= Show;

        void Show(string text, Vector3 worldPosition)
        {
            int i = next++ % poolSize;
            pool[i].text = text;
            pool[i].enabled = true;
            pool[i].color = text.StartsWith("DELETED") ? new Color(1f, 0.4f, 0.4f) : new Color(1f, 0.55f, 1f);
            anchor[i] = worldPosition;
            age[i] = 0f;
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            for (int i = 0; i < poolSize; i++)
            {
                if (!pool[i].enabled) continue;
                age[i] += Time.unscaledDeltaTime;
                if (age[i] >= lifetime)
                {
                    pool[i].enabled = false;
                    continue;
                }
                var screen = cam.WorldToScreenPoint(anchor[i] + Vector3.up * age[i] * 0.4f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out var local);
                float half = pool[i].preferredWidth * 0.5f + 12f;
                float limit = rect.rect.width * 0.5f;
                local.x = Mathf.Clamp(local.x, -limit + half, Mathf.Max(-limit + half, limit - half));
                pool[i].rectTransform.anchoredPosition = local;
                var c = pool[i].color;
                c.a = Mathf.Clamp01((lifetime - age[i]) / 0.6f) * 0.9f;
                pool[i].color = c;
            }
        }
    }
}
