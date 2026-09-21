using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeuroSlop
{
    /// <summary>
    /// Level flow: intro → encounters → boss → victory → results scene.
    /// Death → game over → "regenerate developer" at the current panel (costs resources).
    /// </summary>
    public class LevelController : MonoBehaviour
    {
        public enum State { Playing, GameOver, Victory }

        [SerializeField] PlayerController player;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] EncounterZone[] encounters;
        [SerializeField] string resultsScene = "Results";
        [SerializeField] string menuScene = "MainMenu";
        [SerializeField] float gameOverDelay = 1.4f;
        [SerializeField] float introLineInterval = 2.6f;
        [SerializeField] float victoryDelay = 3.5f;

        public static LevelController Instance { get; private set; }
        public static event Action<State> StateChanged;
        public static event Action<EncounterZone> EncounterStarted;
        public static event Action<EncounterZone> EncounterCleared;
        public static event Action<bool> PauseChanged;

        EncounterZone activeEncounter;
        float checkpointX;
        bool canRespawn;

        /// <summary>Build-only Alt+Tab pause; unattended runs (autopilot) switch it off.</summary>
        public static bool PauseOnFocusLoss { get; set; } = true;

        public State Current { get; private set; }
        public bool TutorialVisible { get; private set; } = true;
        public PlayerController Player => player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            StateChanged = null;
            EncounterStarted = null;
            EncounterCleared = null;
            PauseChanged = null;
        }

        void Awake()
        {
            Instance = this;
            foreach (var e in encounters)
            {
                e.Started += OnEncounterStarted;
                e.Completed += OnEncounterCompleted;
            }
            PlayerController.PlayerDied += OnPlayerDied;
        }

        void OnDestroy()
        {
            PlayerController.PlayerDied -= OnPlayerDied;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Music.Play(MusicTrack.Level);
            GenerationCostService.Register(GenerationKind.Scene);
            checkpointX = player.transform.position.x;
            StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            yield return new WaitForSeconds(0.8f);
            foreach (var line in NarrativeTexts.Load().introLines)
            {
                MetaMessages.Post(line);
                yield return new WaitForSeconds(introLineInterval);
            }
        }

        void Update()
        {
            if (Current == State.Playing && !TimeControl.Paused) RunStats.AddTime(Time.unscaledDeltaTime);
            // The game can be paused. The datacenter can't.
            if (Current != State.Victory) GenerationCostService.TickIdle(Time.unscaledDeltaTime);

            if (Current == State.Playing && GameInput.PausePressed())
            {
                SetPaused(!TimeControl.Paused);
            }
            else if (TimeControl.Paused && GameInput.BackPressed())
            {
                SetPaused(false);
                Music.Play(MusicTrack.None);
                SceneManager.LoadScene(menuScene);
                return;
            }

            if (Current == State.GameOver && canRespawn && GameInput.ConfirmPressed()) Respawn();
        }

#if !UNITY_EDITOR
        // The build keeps running in the background (music, meters), but gameplay pauses on Alt+Tab.
        void OnApplicationFocus(bool focused)
        {
            if (!focused && PauseOnFocusLoss && Current == State.Playing && !TimeControl.Paused) SetPaused(true);
        }
#endif

        void SetPaused(bool paused)
        {
            TimeControl.Paused = paused;
            Sfx.Play(SfxId.UiMove);
            PauseChanged?.Invoke(paused);
        }

        void SetState(State next)
        {
            Current = next;
            StateChanged?.Invoke(next);
        }

        void OnEncounterStarted(EncounterZone zone)
        {
            activeEncounter = zone;
            checkpointX = zone.MinX + 1.5f;
            cameraRig.Lock(zone.MinX, zone.MaxX);
            if (zone.IsBoss) Music.Play(MusicTrack.Boss);
            EncounterStarted?.Invoke(zone);
        }

        void OnEncounterCompleted(EncounterZone zone)
        {
            activeEncounter = null;
            cameraRig.Unlock();
            checkpointX = player.transform.position.x;
            TutorialVisible = false;
            EncounterCleared?.Invoke(zone);
            if (zone.IsBoss) StartCoroutine(Victory());
        }

        void OnPlayerDied(PlayerController p)
        {
            if (Current != State.Playing) return;
            StartCoroutine(GameOverRoutine());
        }

        IEnumerator GameOverRoutine()
        {
            MetaMessages.Post(NarrativeTexts.Pick(NarrativeTexts.Load().deathLines), MessageKind.Error);
            yield return new WaitForSeconds(gameOverDelay);
            SetState(State.GameOver);
            yield return new WaitForSecondsRealtime(0.6f);
            canRespawn = true;
        }

        void Respawn()
        {
            canRespawn = false;
            RunStats.AddDeath();
            GenerationCostService.Register(GenerationKind.Regeneration);
            CorruptionSystem.TriggerBurst(0.8f);
            if (activeEncounter != null) activeEncounter.ResetForRespawn();
            player.Revive(new Vector2(checkpointX, 0.1f));
            SetState(State.Playing);
            MetaMessages.Post("developer_v" + (RunStats.Deaths + 1) + " сгенерирован. Отличия от оригинала: не обнаружены.", MessageKind.System);
        }

        IEnumerator Victory()
        {
            SetState(State.Victory);
            RunStats.Completed = true;
            player.ControlLocked = true;
            yield return new WaitForSecondsRealtime(victoryDelay);
            Music.Play(MusicTrack.None);
            SceneManager.LoadScene(resultsScene);
        }
    }
}
