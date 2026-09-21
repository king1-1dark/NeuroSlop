using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NeuroSlop
{
    /// <summary>
    /// Bottom-left log where the agent and the system talk. Typewriter output, oldest line on
    /// top, lines fade out after a while.
    /// </summary>
    public class AgentConsole : MonoBehaviour
    {
        [SerializeField] Text[] lines;
        [SerializeField] float charsPerSecond = 55f;
        [SerializeField] float lineLifetime = 9f;

        struct Entry
        {
            public string text;
            public Color color;
            public float shownChars;
            public float age;
        }

        readonly List<Entry> entries = new();
        readonly Queue<(string, MessageKind)> pending = new();
        string agentName;

        void Awake() => agentName = NarrativeTexts.Load().agentName;

        void OnEnable() => MetaMessages.Posted += OnPosted;
        void OnDisable() => MetaMessages.Posted -= OnPosted;

        void OnPosted(string text, MessageKind kind) => pending.Enqueue((text, kind));

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool typing = entries.Count > 0 && entries[^1].shownChars < entries[^1].text.Length;
            if (!typing && pending.Count > 0)
            {
                var (text, kind) = pending.Dequeue();
                entries.Add(new Entry { text = Format(text, kind), color = ColorFor(kind) });
                if (entries.Count > lines.Length) entries.RemoveAt(0);
                if (kind == MessageKind.Error) CorruptionSystem.TriggerBurst(0.25f);
            }

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                e.shownChars = Mathf.Min(e.text.Length, e.shownChars + charsPerSecond * dt * (pending.Count > 2 ? 3f : 1f));
                e.age += dt;
                entries[i] = e;
                if (e.age > lineLifetime && e.shownChars >= e.text.Length) entries.RemoveAt(i);
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (i < entries.Count)
                {
                    var e = entries[i];
                    lines[i].text = e.text.Substring(0, (int)e.shownChars) + (e.shownChars < e.text.Length ? "_" : "");
                    var col = e.color;
                    col.a *= Mathf.Clamp01((lineLifetime - e.age) / 1.5f);
                    lines[i].color = col;
                }
                else lines[i].text = string.Empty;
            }
        }

        string Format(string text, MessageKind kind) => kind switch
        {
            MessageKind.Agent => $"[{agentName}] {text}",
            MessageKind.System => $"> {text}",
            MessageKind.Prompt => $"prompt: {text}",
            MessageKind.Error => $"!! {text}",
            _ => text
        };

        static Color ColorFor(MessageKind kind) => kind switch
        {
            MessageKind.Agent => new Color(0.45f, 1f, 0.55f),
            MessageKind.System => new Color(0.7f, 0.85f, 1f),
            MessageKind.Prompt => new Color(1f, 0.6f, 1f),
            MessageKind.Error => new Color(1f, 0.35f, 0.3f),
            _ => Color.white
        };
    }
}
