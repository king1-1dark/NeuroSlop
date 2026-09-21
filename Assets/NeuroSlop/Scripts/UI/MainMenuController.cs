using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeuroSlop
{
    /// <summary>Title screen: the prompt that "generated" this game is typed out, confirm starts a run.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] string levelScene = "Level_01";
        [SerializeField] Text promptLine;
        [SerializeField] Text pressStart;
        [SerializeField, TextArea] string prompt =
            "prompt: 2D beat'em up, 16-bit, comix zone style, indie developer hero, neon street, masterpiece, best quality, cheap, fast";
        [SerializeField] float charsPerSecond = 40f;

        float time;
        bool starting;

        void Start()
        {
            Music.Play(MusicTrack.Menu);
            GenerationCostService.Register(GenerationKind.Text);
        }

        void Update()
        {
            time += Time.unscaledDeltaTime;
            int chars = Mathf.Min(prompt.Length, (int)(time * charsPerSecond));
            promptLine.text = prompt.Substring(0, chars) + (Mathf.Repeat(time * 2f, 1f) < 0.5f ? "_" : " ");
            pressStart.enabled = !starting ? Mathf.Repeat(time * 1.5f, 1f) < 0.7f : Mathf.Repeat(time * 12f, 1f) < 0.5f;

            if (starting || time < 0.5f) return;
            // Start on a gamepad means "start"; Esc on a keyboard means "quit".
            var pause = GameInput.Find("Player/Pause");
            bool pausePressed = pause != null && pause.WasPressedThisFrame();
            bool padStart = pausePressed && pause.activeControl?.device is Gamepad;
            if (GameInput.ConfirmPressed() || padStart) StartCoroutine(Begin());
            else if (pausePressed || GameInput.BackPressed()) Quit();
        }

        IEnumerator Begin()
        {
            starting = true;
            Sfx.Play(SfxId.UiConfirm);
            CorruptionSystem.TriggerBurst(1f);
            yield return new WaitForSecondsRealtime(0.7f);
            RunStats.Reset();
            Music.Play(MusicTrack.None);
            SceneManager.LoadScene(levelScene);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
