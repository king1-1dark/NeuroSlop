using UnityEngine;
using UnityEngine.UI;

namespace NeuroSlop
{
    /// <summary>
    /// Reads gameplay state (Health, RunStats, boss, level events) and shows it.
    /// Gameplay never references this class.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] UIBar playerBar;
        [SerializeField] GlitchText playerName;

        [Header("Last hit enemy")]
        [SerializeField] GameObject targetPanel;
        [SerializeField] GlitchText targetName;
        [SerializeField] UIBar targetBar;

        [Header("Usage monitor")]
        [SerializeField] GlitchText generations;
        [SerializeField] GlitchText energy;
        [SerializeField] GlitchText water;
        [SerializeField] GlitchText timer;
        [SerializeField] Text generationPopup;

        [Header("Boss")]
        [SerializeField] GameObject bossPanel;
        [SerializeField] GlitchText bossName;
        [SerializeField] UIBar bossBar;

        [Header("Messages")]
        [SerializeField] Text headline;
        [SerializeField] Text goArrow;
        [SerializeField] Text controlsHint;

        [Header("Screens")]
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] Text gameOverCost;
        [SerializeField] GameObject pausePanel;

        PlayerController player;
        BossController boss;
        Health target;
        float targetUntil;
        float headlineUntil;
        float goUntil;
        float popupUntil;
        int popupCount;
        float popupWh;

        void OnEnable()
        {
            Hits.Landed += OnHit;
            BossController.Activated += OnBossActivated;
            RunStats.Generated += OnGenerated;
            MetaMessages.Headline += OnHeadline;
            LevelController.StateChanged += OnStateChanged;
            LevelController.EncounterCleared += OnEncounterCleared;
            LevelController.PauseChanged += OnPauseChanged;
        }

        void OnDisable()
        {
            Hits.Landed -= OnHit;
            BossController.Activated -= OnBossActivated;
            RunStats.Generated -= OnGenerated;
            MetaMessages.Headline -= OnHeadline;
            LevelController.StateChanged -= OnStateChanged;
            LevelController.EncounterCleared -= OnEncounterCleared;
            LevelController.PauseChanged -= OnPauseChanged;
        }

        void Start()
        {
            var texts = NarrativeTexts.Load();
            player = PlayerController.Instance;
            playerName.Value = texts.heroName;
            controlsHint.text = texts.controlsHint;
            targetPanel.SetActive(false);
            bossPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            pausePanel.SetActive(false);
            headline.enabled = false;
            goArrow.enabled = false;
            generationPopup.enabled = false;
        }

        void Update()
        {
            float now = Time.unscaledTime;

            if (player != null) playerBar.Set(player.Health.Normalized);

            bool showTarget = target != null && now < targetUntil;
            if (targetPanel.activeSelf != showTarget) targetPanel.SetActive(showTarget);
            if (showTarget) targetBar.Set(target.Normalized);

            generations.Value = RunStats.Generations.ToString("0000");
            energy.Value = GenerationCostService.FormatEnergy(RunStats.EnergyWh);
            water.Value = GenerationCostService.FormatWater(RunStats.WaterMl);
            timer.Value = GenerationCostService.FormatTime(RunStats.TimeSeconds);

            if (generationPopup.enabled)
            {
                float k = (popupUntil - now) / 1.2f;
                generationPopup.enabled = k > 0f;
                generationPopup.color = new Color(1f, 0.85f, 0.2f, Mathf.Clamp01(k * 2f));
            }

            if (boss != null && bossPanel.activeSelf)
            {
                bossBar.Set(boss.Health.Normalized);
                bossName.Value = $"{boss.DisplayName}   [phase {boss.PhaseIndex + 1}]";
                if (boss.Health.IsDead && now > headlineUntil) bossPanel.SetActive(false);
            }

            if (headline.enabled && now > headlineUntil) headline.enabled = false;
            goArrow.enabled = now < goUntil && Mathf.Repeat(now * 2.5f, 1f) < 0.65f;

            var level = LevelController.Instance;
            bool hint = level != null && level.TutorialVisible;
            if (controlsHint.enabled != hint) controlsHint.enabled = hint;
        }

        void OnHit(Health victim, DamageInfo info, Team attacker)
        {
            if (attacker != Team.Player || victim.GetComponent<BossController>() != null) return;
            target = victim;
            targetUntil = Time.unscaledTime + 2.5f;
            targetName.Value = victim.DisplayName;
            targetBar.Set(victim.Normalized + (float)info.Amount / victim.Max, true);
            targetBar.Set(victim.Normalized);
        }

        void OnBossActivated(BossController b)
        {
            boss = b;
            bossPanel.SetActive(true);
            bossBar.Set(1f, true);
        }

        void OnGenerated(GenerationKind kind, int count, float wh)
        {
            if (Time.unscaledTime > popupUntil)
            {
                popupCount = 0;
                popupWh = 0f;
            }
            popupCount += count;
            popupWh += wh;
            popupUntil = Time.unscaledTime + 1.2f;
            generationPopup.text = $"+{popupCount} gen  +{popupWh:0.0} Wh";
            generationPopup.enabled = true;
        }

        void OnHeadline(string text, float duration)
        {
            headline.text = text;
            headline.enabled = true;
            headlineUntil = Time.unscaledTime + duration;
        }

        void OnEncounterCleared(EncounterZone zone)
        {
            if (!zone.IsBoss) goUntil = Time.unscaledTime + 3f;
        }

        void OnStateChanged(LevelController.State state)
        {
            gameOverPanel.SetActive(state == LevelController.State.GameOver);
            if (state == LevelController.State.GameOver)
            {
                var cfg = GenerationCostService.Config;
                float wh = cfg.regenerationWh;
                gameOverCost.text = $"стоимость регенерации: +1 генерация · +{wh:0} Wh · +{wh * cfg.waterMlPerWh:0} ml воды";
            }
        }

        void OnPauseChanged(bool paused) => pausePanel.SetActive(paused);
    }
}
