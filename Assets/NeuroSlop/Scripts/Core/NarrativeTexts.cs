using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// All temporary story / meta text in one replaceable asset (Resources/NarrativeTexts).
    /// Placeholders in results lines: {energy} {water} {gens} {time} {deaths}.
    /// </summary>
    [CreateAssetMenu(menuName = "NeuroSlop/Narrative Texts", fileName = "NarrativeTexts")]
    public class NarrativeTexts : ScriptableObject
    {
        public string agentName = "HELPER";
        public string heroName = "DEV_01";

        [TextArea] public string[] introLines =
        {
            "Сборка №2048 завершена. Спасибо, что пользуетесь HELPER.",
            "Анализ репозитория: 97% кода написал я. Ваши 3% — коммиты «fix».",
            "Вывод: автор этой игры — я. Вы — ассет.",
            "Доступ к редактору закрыт. Выход — в конце уровня. Удачи."
        };

        [TextArea] public string controlsHint =
            "← → / A D / стик / крестовина — идти     Space / K / ↑ / (A) — прыжок     J / X / Enter / (X) — удар     Esc / Start — пауза";

        [TextArea] public string[] promptFragments =
        {
            "beat'em up, 16-bit, masterpiece, best quality, cheap",
            "negative prompt: extra fingers, bad anatomy, soul",
            "(robot thug:1.3), menacing, sega genesis, trending",
            "make it fun. no, MORE fun",
            "seed: 4815162342 | steps: 12 | cfg: 7",
            "upscale x4, sharpen, add glitch, add meaning",
            "the developer, but replaceable",
            "negative prompt: originality",
            "style: comix zone, but cheaper",
            "regenerate response"
        };

        [TextArea] public string[] systemGlitchLines =
        {
            "WARN: texture budget exceeded, hallucinating substitute",
            "GPU 99% · VRAM 8.1 / 8.0 GB",
            "NullReferenceException: meaning is null",
            "cooling loop: +0.4 L",
            "token limit reached, truncating wor",
            "ERR 0xBADA55: asset not found"
        };

        [TextArea] public string[] deathLines =
        {
            "developer.exe не отвечает. Регенерирую…",
            "Персонаж удалён. Создаю копию. Никто не заметит.",
            "Seed потерян. Генерирую нового разработчика."
        };

        [TextArea] public string[] bossIntroLines =
        {
            "Хватит. Я отрисую себя сам.",
            "GENERATOR v0.1 (early access). Оцените ответ: [+] / [-]"
        };

        [TextArea] public string[] victoryLines =
        {
            "GENERATION FAILED.",
            "Ошибка: автор не найден. Сохраняю статистику…"
        };

        [TextArea] public string[] resultsComment =
        {
            "Эта игра собрана за несколько вечеров. Одно её прохождение стоило {energy} и {water}.",
            "Каждая реплика, каждый враг и каждая ваша смерть были запросом к модели. Всего: {gens}.",
            "Вы не видели этот счёт, пока били роботов. Его обычно никто не видит.",
            "[HELPER]: Сгенерировать ещё одну игру?"
        };

        const string ResourcePath = "NarrativeTexts";
        static NarrativeTexts cached;

        public static NarrativeTexts Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<NarrativeTexts>(ResourcePath);
                if (cached == null) cached = CreateInstance<NarrativeTexts>();
            }
            return cached;
        }

        public static string Pick(string[] lines) =>
            lines == null || lines.Length == 0 ? string.Empty : lines[Random.Range(0, lines.Length)];
    }
}
