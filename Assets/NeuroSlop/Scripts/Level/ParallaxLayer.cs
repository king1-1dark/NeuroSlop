using UnityEngine;

namespace NeuroSlop
{
    /// <summary>Moves a (tiled) background layer at a fraction of the camera speed.</summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float factor = 0.3f;
        [SerializeField] Transform cameraTransform;

        Vector3 origin;

        void Awake() => origin = transform.position;

        void LateUpdate()
        {
            if (cameraTransform == null) return;
            transform.position = new Vector3(origin.x + cameraTransform.position.x * factor, origin.y, origin.z);
        }
    }
}
