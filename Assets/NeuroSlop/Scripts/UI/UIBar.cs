using UnityEngine;

namespace NeuroSlop
{
    /// <summary>Horizontal bar driven by anchors; a trailing segment shows recent damage.</summary>
    public class UIBar : MonoBehaviour
    {
        [SerializeField] RectTransform fill;
        [SerializeField] RectTransform trail;
        [SerializeField] float trailSpeed = 0.7f;

        float value = 1f;
        float trailValue = 1f;

        public void Set(float normalized, bool instant = false)
        {
            value = Mathf.Clamp01(normalized);
            if (instant || value > trailValue) trailValue = value;
        }

        void Update()
        {
            trailValue = Mathf.MoveTowards(trailValue, value, trailSpeed * Time.unscaledDeltaTime);
            if (fill != null) fill.anchorMax = new Vector2(value, 1f);
            if (trail != null) trail.anchorMax = new Vector2(trailValue, 1f);
        }
    }
}
