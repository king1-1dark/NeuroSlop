using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace NeuroSlop
{
    /// <summary>
    /// QA tool (editor / development builds only — Debug.isDebugBuild; F9 toggles): plays the slice end to end by
    /// pressing keys on a virtual keyboard, so it goes through the real Input System path —
    /// menu → panels → boss → results. Not part of the game.
    /// </summary>
    public class Autopilot : MonoBehaviour
    {
        static Autopilot instance;
        Keyboard keyboard;
        Gamepad gamepad;
        readonly HashSet<Key> held = new();
        int frame;

        public static bool Running => instance != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallToggle()
        {
            if (!Debug.isDebugBuild) return;
            var go = new GameObject("[AutopilotToggle]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<AutopilotToggle>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autopilot") >= 0) SetEnabled(true);
        }

        public static void SetEnabled(bool value)
        {
            if (value == Running) return;
            if (value)
            {
                var go = new GameObject("[Autopilot]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Autopilot>();
                MetaMessages.Post("autopilot: ON — агент играет за вас. Это тоже стоит энергии.", MessageKind.System);
            }
            else
            {
                Destroy(instance.gameObject);
                instance = null;
            }
        }

        void OnEnable()
        {
            // -autopilot-gamepad drives a virtual gamepad instead, to cover the pad bindings.
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autopilot-gamepad") >= 0)
                gamepad = InputSystem.AddDevice<Gamepad>("AutopilotGamepad");
            else
                keyboard = InputSystem.AddDevice<Keyboard>("AutopilotKeyboard");
            shotsDir = Arg("-autopilot-shots");
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            LevelController.PauseOnFocusLoss = false;
        }

        void OnDisable()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            keyboard = null;
            gamepad = null;
            LevelController.PauseOnFocusLoss = true;
        }

        void Update()
        {
            frame++;
            held.Clear();
            Report();
            bool tap = frame % 4 < 2; // press/release so WasPressedThisFrame fires

            var level = LevelController.Instance;
            if (level == null)
            {
                // Title screen: confirm. Results screen: stop there (the run is complete).
                if (tap && FindAnyObjectByType<ResultsController>() == null) held.Add(Key.Enter);
            }
            else if (level.Current == LevelController.State.GameOver)
            {
                if (tap) held.Add(Key.Enter);
            }
            else if (level.Current == LevelController.State.Playing && !TimeControl.Paused)
            {
                Fight(level.Player, tap);
            }

            if (gamepad != null)
            {
                var pad = new GamepadState { leftStick = new Vector2((held.Contains(Key.D) ? 1f : 0f) - (held.Contains(Key.A) ? 1f : 0f), 0f) };
                if (held.Contains(Key.Space) || held.Contains(Key.Enter)) pad = pad.WithButton(GamepadButton.South);
                if (held.Contains(Key.J)) pad = pad.WithButton(GamepadButton.West);
                InputSystem.QueueStateEvent(gamepad, pad);
                return;
            }
            var state = new KeyboardState();
            foreach (var k in held) state.Set(k, true);
            InputSystem.QueueStateEvent(keyboard, state);
        }

        string lastScene;
        string lastState;
        float nextHeartbeat;
        float resultsSince = -1f;
        string shotsDir;
        int shotIndex;
        float pendingShotAt = -1f;
        string pendingShotLabel;

        static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        void Shot(string label)
        {
            if (shotsDir == null) return;
            System.IO.Directory.CreateDirectory(shotsDir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotsDir, $"{shotIndex++:00}_{label}.png"));
        }

        /// <summary>Progress log for unattended runs (Player.log / -logFile).</summary>
        void Report()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var level = LevelController.Instance;
            string state = level != null ? level.Current.ToString() : "-";
            if (scene != lastScene || state != lastState)
            {
                Debug.Log($"[Autopilot] t={Time.unscaledTime:0.0} scene={scene} state={state} {Stats()}");
                lastScene = scene;
                lastState = state;
                resultsSince = FindAnyObjectByType<ResultsController>() != null ? Time.unscaledTime : -1f;
                pendingShotAt = Time.unscaledTime + (resultsSince >= 0f ? 14f : 1.2f);
                pendingShotLabel = scene + "_" + state;
            }
            if (pendingShotAt >= 0f && Time.unscaledTime >= pendingShotAt)
            {
                pendingShotAt = -1f;
                Shot(pendingShotLabel);
            }
            if (Time.unscaledTime >= nextHeartbeat)
            {
                nextHeartbeat = Time.unscaledTime + 10f;
                Shot("heartbeat");
                var p = PlayerController.Instance;
                var boss = FindAnyObjectByType<BossController>();
                Debug.Log($"[Autopilot] heartbeat t={Time.unscaledTime:0.0} px={(p ? p.transform.position.x : -1f):0.0} hp={(p ? p.Health.Current : -1)} " +
                          $"boss={(boss && boss.IsActive ? boss.Health.Current + "/phase" + (boss.PhaseIndex + 1) : "-")} corruption={CorruptionSystem.Level:0.00} tier={CorruptionSystem.Tier} {Stats()}");
            }
            if (resultsSince >= 0f && Time.unscaledTime - resultsSince > 20f && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autopilot-quit") >= 0)
            {
                Debug.Log($"[Autopilot] DONE completed={RunStats.Completed} {Stats()}");
                Application.Quit();
            }
        }

        static string Stats() =>
            $"time={RunStats.TimeSeconds:0.0}s gens={RunStats.Generations} energy={RunStats.EnergyWh:0.0}Wh water={RunStats.WaterMl:0}ml kills={RunStats.Kills} deaths={RunStats.Deaths}";

        void Fight(PlayerController player, bool tap)
        {
            if (player == null || player.IsDead) return;
            float px = player.transform.position.x;

            // Dodge the beam: jump shortly before it fires.
            foreach (var beam in FindObjectsByType<BeamAttack>())
                if (beam.TelegraphRemaining >= 0f && beam.TelegraphRemaining < 0.22f) held.Add(Key.Space);
            if (held.Contains(Key.Space)) return;

            // Step off drop markers.
            foreach (var m in FindObjectsByType<SpriteRenderer>())
            {
                if (!m.name.StartsWith("DropMarker") || Mathf.Abs(m.transform.position.x - px) > 1.6f) continue;
                held.Add(m.transform.position.x > px ? Key.A : Key.D);
                return;
            }

            Transform target = null;
            float best = float.MaxValue;
            foreach (var e in EnemyController.Active)
            {
                if (e.IsDead) continue;
                float d = Mathf.Abs(e.transform.position.x - px);
                if (d < best) { best = d; target = e.transform; }
            }
            var boss = FindAnyObjectByType<BossController>();
            if (boss != null && boss.IsActive && !boss.Health.IsDead)
            {
                float d = Mathf.Abs(boss.transform.position.x - 1.9f - px);
                if (d < best) { best = d; target = boss.transform; }
            }

            if (target == null)
            {
                held.Add(Key.D);
                return;
            }

            float tx = boss != null && target == boss.transform ? target.position.x - 2.4f : target.position.x;
            float dx = tx - px;
            if (Mathf.Abs(dx) > 1.25f) held.Add(dx > 0f ? Key.D : Key.A);
            else
            {
                if (Mathf.Sign(dx) != player.Facing && Mathf.Abs(dx) > 0.2f) held.Add(dx > 0f ? Key.D : Key.A);
                if (tap) held.Add(Key.J);
            }
        }
    }

    class AutopilotToggle : MonoBehaviour
    {
        void Update()
        {
            foreach (var device in InputSystem.devices)
                if (device is Keyboard kb && kb.f9Key.wasPressedThisFrame)
                {
                    Autopilot.SetEnabled(!Autopilot.Running);
                    return;
                }
        }
    }
}
