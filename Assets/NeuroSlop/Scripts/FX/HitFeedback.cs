using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// Central combat feedback: listens to every landed hit and answers with spark, sound,
    /// hit-stop and camera shake. Attacks themselves stay free of feedback code.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        [SerializeField] TimedEffect sparkPrefab;
        [SerializeField] float lightHitStop = 0.045f;
        [SerializeField] float heavyHitStop = 0.1f;

        void OnEnable() => Hits.Landed += OnHit;
        void OnDisable() => Hits.Landed -= OnHit;

        void OnHit(Health target, DamageInfo info, Team attacker)
        {
            if (sparkPrefab != null)
            {
                var spark = Instantiate(sparkPrefab, new Vector3(info.Point.x, info.Point.y, -1f), Quaternion.identity);
                spark.SetScale(info.Heavy ? 1.6f : 1f);
            }

            if (attacker == Team.Player)
            {
                Sfx.Play(info.Heavy ? SfxId.HitHeavy : SfxId.Hit);
                TimeControl.HitStop(info.Heavy ? heavyHitStop : lightHitStop);
                CameraRig.Shake(info.Heavy ? 0.18f : 0.07f, 0.12f);
            }
            else
            {
                Sfx.Play(SfxId.Hurt);
                TimeControl.HitStop(0.07f);
                CameraRig.Shake(0.22f, 0.2f);
                CorruptionSystem.TriggerBurst(0.35f);
            }
        }
    }
}
