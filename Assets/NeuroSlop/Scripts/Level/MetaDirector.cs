using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// The agent "interfering": random prompt fragments and system errors that surface more
    /// often as corruption rises. Tier 0 — rare odd texts, tier 2+ — constant noise.
    /// </summary>
    public class MetaDirector : MonoBehaviour
    {
        [SerializeField] Vector2 intervalAtZero = new(22f, 34f);
        [SerializeField] Vector2 intervalAtMax = new(4f, 8f);

        float nextTime;

        void Start() => Schedule();

        void Update()
        {
            if (Time.time < nextTime) return;
            Schedule();

            var texts = NarrativeTexts.Load();
            var player = PlayerController.Instance;
            int tier = CorruptionSystem.Tier;

            if (tier >= 1 && Random.value < 0.5f)
            {
                MetaMessages.Post(NarrativeTexts.Pick(texts.systemGlitchLines), MessageKind.Error);
                CorruptionSystem.TriggerBurst(0.3f + 0.15f * tier);
                Sfx.Play(SfxId.Glitch, 0.5f);
            }
            else if (player != null)
            {
                var pos = player.transform.position + new Vector3(Random.Range(-5f, 5f), Random.Range(3f, 5.5f), 0f);
                MetaMessages.PromptAt(NarrativeTexts.Pick(texts.promptFragments), pos);
            }
        }

        void Schedule()
        {
            float c = CorruptionSystem.Intensity;
            float min = Mathf.Lerp(intervalAtZero.x, intervalAtMax.x, c);
            float max = Mathf.Lerp(intervalAtZero.y, intervalAtMax.y, c);
            nextTime = Time.time + Random.Range(min, max);
        }
    }
}
