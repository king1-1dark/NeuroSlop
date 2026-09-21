using System;
using UnityEngine;

namespace NeuroSlop
{
    public enum MessageKind { Agent, System, Prompt, Error }

    /// <summary>
    /// Event bus for the meta layer: gameplay posts, UI displays. Every line the agent "says"
    /// is a text generation and is billed as such.
    /// </summary>
    public static class MetaMessages
    {
        public static event Action<string, MessageKind> Posted;
        public static event Action<string, Vector3> WorldPromptPosted;
        public static event Action<string, float> Headline;   // big centre text, duration

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Posted = null;
            WorldPromptPosted = null;
            Headline = null;
        }

        public static void Post(string text, MessageKind kind = MessageKind.Agent)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (kind == MessageKind.Agent) GenerationCostService.Register(GenerationKind.Text);
            Posted?.Invoke(text, kind);
        }

        public static void PromptAt(string text, Vector3 worldPosition)
        {
            if (!string.IsNullOrEmpty(text)) WorldPromptPosted?.Invoke(text, worldPosition);
        }

        public static void ShowHeadline(string text, float duration = 2f) => Headline?.Invoke(text, duration);
    }
}
