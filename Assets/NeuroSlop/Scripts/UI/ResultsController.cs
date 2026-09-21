using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeuroSlop
{
    /// <summary>
    /// Final screen: time, generations, energy, water (with everyday comparisons), then the
    /// agent's meta comment about what the run actually cost.
    /// </summary>
    public class ResultsController : MonoBehaviour
    {
        [SerializeField] Text title;
        [SerializeField] Text[] labels;
        [SerializeField] Text[] values;
        [SerializeField] Text comment;
        [SerializeField] Text footer;
        [SerializeField] string levelScene = "Level_01";
        [SerializeField] string menuScene = "MainMenu";
        [SerializeField] float charsPerSecond = 45f;

        bool ready;
        bool leaving;

        void Start()
        {
            Music.Play(MusicTrack.Results);
            foreach (var l in labels) l.enabled = false;
            foreach (var v in values) v.enabled = false;
            comment.text = string.Empty;
            footer.enabled = false;
            title.text = RunStats.Completed ? "ПРОХОЖДЕНИЕ ЗАВЕРШЕНО" : "ПРОХОЖДЕНИЕ НЕ ЗАВЕРШЕНО";
            StartCoroutine(Reveal());
        }

        IEnumerator Reveal()
        {
            var cfg = GenerationCostService.Config;
            // Snapshot: the results screen itself is still running on someone's GPU.
            float time = RunStats.TimeSeconds, wh = RunStats.EnergyWh, ml = RunStats.WaterMl;
            int gens = RunStats.Generations;

            string[] names = { "Время прохождения", "Генераций", "Энергия", "Вода", "Регенераций разработчика", "Удалено ассетов" };
            string[] finals =
            {
                GenerationCostService.FormatTime(time),
                gens.ToString(),
                $"{GenerationCostService.FormatEnergy(wh)}   (≈ {wh / cfg.phoneChargeWh:0.0} зарядки смартфона)",
                $"{GenerationCostService.FormatWater(ml)}   (≈ {ml / cfg.glassOfWaterMl:0.0} стакана)",
                RunStats.Deaths.ToString(),
                RunStats.Kills.ToString()
            };

            yield return new WaitForSecondsRealtime(0.8f);
            int rows = Mathf.Min(labels.Length, values.Length, names.Length);
            for (int i = 0; i < rows; i++)
            {
                labels[i].text = names[i];
                labels[i].enabled = true;
                values[i].enabled = true;
                float t = 0f;
                while (t < 0.5f)
                {
                    t += Time.unscaledDeltaTime;
                    values[i].text = Scramble(finals[i], t / 0.5f);
                    yield return null;
                }
                values[i].text = finals[i];
                Sfx.Play(SfxId.UiMove);
                yield return new WaitForSecondsRealtime(0.25f);
            }

            yield return new WaitForSecondsRealtime(0.6f);
            GenerationCostService.Register(GenerationKind.Text);
            var sb = new System.Text.StringBuilder();
            foreach (var raw in NarrativeTexts.Load().resultsComment)
            {
                string line = raw.Replace("{energy}", GenerationCostService.FormatEnergy(wh))
                                 .Replace("{water}", GenerationCostService.FormatWater(ml) + " воды")
                                 .Replace("{gens}", gens.ToString())
                                 .Replace("{time}", GenerationCostService.FormatTime(time))
                                 .Replace("{deaths}", RunStats.Deaths.ToString());
                for (int c = 1; c <= line.Length; c++)
                {
                    comment.text = sb + line.Substring(0, c) + "_";
                    yield return new WaitForSecondsRealtime(1f / charsPerSecond);
                }
                sb.Append(line).Append("\n\n");
                comment.text = sb.ToString();
                yield return new WaitForSecondsRealtime(0.5f);
            }

            footer.enabled = true;
            ready = true;
        }

        static string Scramble(string final, float k)
        {
            var chars = final.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (char.IsDigit(chars[i]) && Random.value > k) chars[i] = (char)('0' + Random.Range(0, 10));
            return new string(chars);
        }

        void Update()
        {
            if (leaving) return;
            if (!ready)
            {
                // Let impatient players skip the reveal.
                if (GameInput.ConfirmPressed()) charsPerSecond = 400f;
                return;
            }
            if (GameInput.ConfirmPressed()) StartCoroutine(Leave(levelScene, true));
            else if (GameInput.BackPressed() || GameInput.PausePressed()) StartCoroutine(Leave(menuScene, false));
        }

        IEnumerator Leave(string scene, bool newRun)
        {
            leaving = true;
            Sfx.Play(SfxId.UiConfirm);
            CorruptionSystem.TriggerBurst(1f);
            yield return new WaitForSecondsRealtime(0.6f);
            if (newRun) RunStats.Reset();
            Music.Play(MusicTrack.None);
            SceneManager.LoadScene(scene);
        }
    }
}
