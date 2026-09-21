using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// The only writer of Time.timeScale: pause, hit-stop and slow motion never fight each other.
    /// </summary>
    public class TimeControl : MonoBehaviour
    {
        static TimeControl instance;
        static float hitStopUntil;
        static float slowMoUntil;
        static float slowMoScale = 1f;

        public static bool Paused { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            Paused = false;
            hitStopUntil = 0f;
            slowMoUntil = 0f;
            slowMoScale = 1f;
        }

        void Awake()
        {
            instance = this;
            Paused = false;
            hitStopUntil = slowMoUntil = 0f;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            Paused = false;
            Time.timeScale = 1f;
        }

        public static void HitStop(float seconds) => hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);

        public static void SlowMotion(float scale, float seconds)
        {
            slowMoScale = scale;
            slowMoUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            float scale = 1f;
            if (now < slowMoUntil) scale = slowMoScale;
            if (now < hitStopUntil) scale = 0.02f;
            if (Paused) scale = 0f;
            Time.timeScale = scale;
        }
    }
}
