using UnityEngine;

namespace NeuroSlop
{
    /// <summary>Blob shadow snapped to the ground under the character: makes jumps readable.</summary>
    public class GroundShadow : MonoBehaviour
    {
        [SerializeField] Transform owner;
        [SerializeField] float maxDistance = 12f;
        [SerializeField] float baseScale = 1f;

        SpriteRenderer sr;

        void Awake() => sr = GetComponent<SpriteRenderer>();

        void LateUpdate()
        {
            if (owner == null) return;
            var origin = (Vector2)owner.position + Vector2.up * 0.2f;
            var hit = Physics2D.Raycast(origin, Vector2.down, maxDistance, Layers.GroundMask);
            if (!hit)
            {
                sr.enabled = false;
                return;
            }
            sr.enabled = true;
            float height = Mathf.Max(0f, owner.position.y - hit.point.y);
            float k = Mathf.Clamp01(1f - height * 0.12f);
            transform.position = new Vector3(owner.position.x, hit.point.y, transform.position.z);
            transform.localScale = new Vector3(baseScale * Mathf.Lerp(0.5f, 1f, k), baseScale, 1f);
            var c = sr.color;
            c.a = Mathf.Lerp(0.2f, 0.55f, k);
            sr.color = c;
        }
    }
}
