using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NeuroSlop
{
    /// <summary>
    /// Wraps a UI Text: owners set Value, this component occasionally shows a corrupted version.
    /// Optional tier variants let a label change its wording as the game breaks
    /// ("HP" → "HP (est.)" → "hit_points = ???").
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class GlitchText : MonoBehaviour
    {
        const string Junk = "█▓▒░#@%&$?!<>/\\|_~^01";

        [SerializeField] float sensitivity = 1f;
        [SerializeField] string[] tierVariants;

        Text label;
        string value;
        float glitchUntil;
        float nextCheck;
        readonly StringBuilder sb = new();

        public string Value
        {
            get => value;
            set => this.value = value;
        }

        void Awake()
        {
            label = GetComponent<Text>();
            if (value == null) value = label.text;
        }

        void LateUpdate()
        {
            string source = value;
            if (tierVariants != null && tierVariants.Length > 0)
                source = tierVariants[Mathf.Min(CorruptionSystem.Tier, tierVariants.Length - 1)];

            float c = Mathf.Clamp01(CorruptionSystem.Intensity * sensitivity);
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.1f;
                if (Random.value < c * c * 0.25f) glitchUntil = Time.unscaledTime + Random.Range(0.05f, 0.25f);
            }

            label.text = Time.unscaledTime < glitchUntil ? Corrupt(source, 0.12f + c * 0.35f) : source;
        }

        string Corrupt(string s, float amount)
        {
            if (string.IsNullOrEmpty(s)) return s;
            sb.Clear();
            foreach (char ch in s)
            {
                if (ch != '\n' && ch != ' ' && Random.value < amount) sb.Append(Junk[Random.Range(0, Junk.Length)]);
                else sb.Append(ch);
            }
            if (Random.value < amount * 0.5f) sb.Length = Mathf.Max(1, sb.Length - Random.Range(1, 4));
            return sb.ToString();
        }
    }
}
