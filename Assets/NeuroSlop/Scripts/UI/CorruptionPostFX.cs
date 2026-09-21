using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NeuroSlop
{
    /// <summary>Drives URP post-processing from corruption: colour split, grain, lens warp.</summary>
    [RequireComponent(typeof(Volume))]
    public class CorruptionPostFX : MonoBehaviour
    {
        [SerializeField] float baseAberration = 0.08f;
        [SerializeField] float maxAberration = 0.9f;
        [SerializeField] float maxGrain = 0.7f;
        [SerializeField] float burstDistortion = -0.35f;

        ChromaticAberration aberration;
        FilmGrain grain;
        LensDistortion distortion;

        void Awake()
        {
            var profile = GetComponent<Volume>().profile;
            profile.TryGet(out aberration);
            profile.TryGet(out grain);
            profile.TryGet(out distortion);
        }

        void Update()
        {
            float c = CorruptionSystem.Intensity;
            float spike = CorruptionSystem.Spike;
            if (aberration != null) aberration.intensity.value = Mathf.Clamp01(baseAberration + c * c * maxAberration + spike * 0.6f);
            if (grain != null) grain.intensity.value = Mathf.Clamp01(0.15f + c * maxGrain);
            if (distortion != null) distortion.intensity.value = spike * burstDistortion * (Random.value < 0.5f ? 1f : -0.6f);
        }
    }
}
