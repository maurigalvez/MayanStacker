using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Presents a <see cref="RunResult.Card"/> on the game-over panel.
///
/// The reveal is paced so a loss lands before it is summarised: the panel waits a beat so
/// the collapse stays on screen, pops in, counts the score up and lets each line arrive in
/// turn. The buttons are live from the moment the card appears - only the beat before it
/// ignores taps, which also stops a mashed drop from hitting Try Again by accident.
///
/// The pop itself is <see cref="UIPopup"/>'s, shared with every other popup; this component
/// owns the beat before it and the lines after it. UIManager activates the panel directly
/// when this card exists, because UIPopup.Show would drive the same CanvasGroup alpha and
/// erase the beat.
///
/// Lives on the Game Over Panel root, so deactivating the panel (restart, main menu) stops
/// the reveal with it. Built and wired by TamalStacker ▸ UI ▸ Set Up Try Again Card.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class RunResultCard : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private TextMeshProUGUI headlineText;
    [SerializeField] private TextMeshProUGUI flavourText;
    [Tooltip("The existing final score label.")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bestLineText;
    [Tooltip("Row holding the three stats; revealed as one line.")]
    [SerializeField] private RectTransform statsRow;
    [SerializeField] private TextMeshProUGUI heightValueText;
    [SerializeField] private TextMeshProUGUI heightLabelText;
    [SerializeField] private TextMeshProUGUI perfectsValueText;
    [SerializeField] private TextMeshProUGUI perfectsLabelText;
    [SerializeField] private TextMeshProUGUI comboValueText;
    [SerializeField] private TextMeshProUGUI comboLabelText;
    [Tooltip("The existing next-goal label.")]
    [SerializeField] private TextMeshProUGUI goalText;

    [Tooltip("Optional Serpent's Edge receipt under the stats. Left empty, it rides above the goal line.")]
    [SerializeField] private TextMeshProUGUI edgeText;

    [Header("Colours")]
    [SerializeField] private Color celebrateColor = new Color(0.79f, 0.64f, 0.29f, 1f); // gold
    [SerializeField] private Color honoredColor = new Color(0.09f, 0.57f, 0.44f, 1f);   // jade
    [SerializeField] private Color brokenColor = new Color(0.66f, 0.25f, 0.16f, 1f);    // clay
    [SerializeField] private Color neutralColor = new Color(0.93f, 0.90f, 0.82f, 1f);   // parchment
    [SerializeField] private Color quietColor = new Color(0.72f, 0.68f, 0.58f, 1f);     // muted

    [Header("Timing (unscaled seconds)")]
    [Tooltip("Beat after the collapse before the card appears.")]
    [SerializeField] private float revealDelay = 0.6f;
    [Tooltip("Share of UIPopup's pop-in to wait before the lines start arriving.")]
    [Range(0f, 1f)]
    [SerializeField] private float linesStartAtPopProgress = 0.6f;
    [Tooltip("Gap between each line arriving.")]
    [SerializeField] private float lineStagger = 0.12f;
    [SerializeField] private float lineDuration = 0.22f;
    [SerializeField] private float countUpDuration = 0.9f;
    [SerializeField] private float celebratePunchScale = 1.2f;
    [SerializeField] private float celebratePunchDuration = 0.3f;

    [Header("Count-up Sound")]
    [SerializeField] private bool playCountTicks = true;
    [SerializeField] private float countTickInterval = 0.06f;
    [SerializeField] private float countStartPitch = 1.0f;
    [SerializeField] private float countEndPitch = 1.6f;

    private CanvasGroup group;
    private GameSoundManager sound;
    private System.Action onReveal;
    private RunResult.Card current;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    private void OnDisable()
    {
        // A reveal cut short (restart during the beat) must not leave the panel invisible or
        // deaf the next time something else shows it.
        if (group == null) group = GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
    }

    /// <summary>
    /// Fills the card and starts the reveal. <paramref name="revealed"/> runs when the card
    /// appears - UIManager hides the HUD there, so the tower stays readable during the beat.
    /// Safe to call again for the same run; the reveal restarts.
    /// </summary>
    public void Show(RunResult.Card result, GameSoundManager soundManager, System.Action revealed)
    {
        if (group == null) group = GetComponent<CanvasGroup>();

        current = result;
        sound = soundManager;
        onReveal = revealed;

        StopAllCoroutines();
        Populate();

        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        foreach (var line in Lines()) SetLineVisible(line, 0f);

        if (!isActiveAndEnabled)
        {
            // Nothing can animate on an inactive panel - show it finished rather than not at all.
            FinishInstantly();
            return;
        }

        StartCoroutine(Reveal());
    }

    private void Populate()
    {
        Color headlineColor = HeadlineColor(current.mood);

        SetText(headlineText, current.headline, headlineColor);
        SetText(flavourText, current.flavour, quietColor);
        SetText(scoreText, FormatScore(0), current.mood == RunResult.Mood.NewBest ? celebrateColor : neutralColor);

        if (bestLineText != null)
        {
            bool hasBest = !string.IsNullOrEmpty(current.bestLine);
            bestLineText.gameObject.SetActive(hasBest);
            if (hasBest) SetText(bestLineText, current.bestLine, current.bestLineCelebrates ? celebrateColor : quietColor);
        }

        SetText(heightValueText, current.height, neutralColor);
        SetText(perfectsValueText, current.perfects, neutralColor);
        SetText(comboValueText, current.combo, neutralColor);
        SetText(heightLabelText, LocalizationManager.Get("result_stat_height"), quietColor);
        SetText(perfectsLabelText, LocalizationManager.Get("result_stat_perfects"), quietColor);
        SetText(comboLabelText, LocalizationManager.Get("result_stat_combo"), quietColor);

        bool hasEdge = !string.IsNullOrEmpty(current.edgeLine);
        if (edgeText != null)
        {
            edgeText.gameObject.SetActive(hasEdge);
            if (hasEdge) SetText(edgeText, current.edgeLine, celebrateColor);
        }

        if (goalText != null)
        {
            goalText.gameObject.SetActive(true);
            goalText.text = current.goal;

            // No dedicated edge label wired yet: carry the edge receipt as a gold line above the goal.
            if (hasEdge && edgeText == null)
            {
                goalText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(celebrateColor) + ">" +
                                current.edgeLine + "</color>\n" + current.goal;
            }
        }
    }

    private IEnumerator Reveal()
    {
        yield return new WaitForSecondsRealtime(revealDelay);

        onReveal?.Invoke();
        group.blocksRaycasts = true;
        group.interactable = true;

        // The same pop every other popup uses, so the card doesn't move like a different game.
        // PopIn (not Show) because the panel is already active - it sat invisible for the beat.
        UIPopup.PopIn(gameObject);
        yield return new WaitForSecondsRealtime(UIPopup.PopInDuration * linesStartAtPopProgress);

        RectTransform[] lines = Lines();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null || !lines[i].gameObject.activeSelf) continue;

            StartCoroutine(FadeLineIn(lines[i]));
            if (scoreText != null && lines[i] == scoreText.rectTransform)
            {
                yield return CountUp();
            }
            else
            {
                yield return new WaitForSecondsRealtime(lineStagger);
            }
        }

        if (current.mood == RunResult.Mood.NewBest || current.mood == RunResult.Mood.Complete || current.bestLineCelebrates)
        {
            if (headlineText != null) StartCoroutine(Punch(headlineText.rectTransform));
            if (bestLineText != null && current.bestLineCelebrates) StartCoroutine(Punch(bestLineText.rectTransform));
        }
    }

    private IEnumerator FadeLineIn(RectTransform line)
    {
        float elapsed = 0f;
        while (elapsed < lineDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetLineVisible(line, EaseOut(Mathf.Clamp01(elapsed / lineDuration)));
            yield return null;
        }
        SetLineVisible(line, 1f);
    }

    private IEnumerator CountUp()
    {
        int target = current.scoreValue;
        if (target <= 0 || countUpDuration <= 0f)
        {
            scoreText.text = FormatScore(target);
            yield break;
        }

        float elapsed = 0f;
        float nextTick = 0f;
        int shown = 0;
        while (elapsed < countUpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / countUpDuration);
            int value = Mathf.FloorToInt(Mathf.Lerp(0f, target, EaseOut(t)));
            if (value != shown)
            {
                shown = value;
                scoreText.text = FormatScore(shown);
            }

            if (playCountTicks && sound != null && elapsed >= nextTick && shown < target)
            {
                sound.PlayScoreCountSound(Mathf.Lerp(countStartPitch, countEndPitch, t));
                nextTick = elapsed + Mathf.Max(0.01f, countTickInterval);
            }
            yield return null;
        }

        scoreText.text = FormatScore(target);
    }

    private IEnumerator Punch(RectTransform target)
    {
        float elapsed = 0f;
        while (elapsed < celebratePunchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / celebratePunchDuration);
            // Up and back in one arc.
            target.localScale = Vector3.one * Mathf.Lerp(1f, celebratePunchScale, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private void FinishInstantly()
    {
        onReveal?.Invoke();
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
        foreach (var line in Lines()) SetLineVisible(line, 1f);
        if (scoreText != null) scoreText.text = FormatScore(current.scoreValue);
    }

    private RectTransform[] Lines()
    {
        return new[]
        {
            headlineText != null ? headlineText.rectTransform : null,
            flavourText != null ? flavourText.rectTransform : null,
            scoreText != null ? scoreText.rectTransform : null,
            bestLineText != null ? bestLineText.rectTransform : null,
            statsRow,
            edgeText != null ? edgeText.rectTransform : null,
            goalText != null ? goalText.rectTransform : null
        };
    }

    private static void SetLineVisible(RectTransform line, float amount)
    {
        if (line == null) return;
        var lineGroup = line.GetComponent<CanvasGroup>();
        if (lineGroup == null) lineGroup = line.gameObject.AddComponent<CanvasGroup>();
        lineGroup.alpha = amount;
        lineGroup.blocksRaycasts = false;
        line.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, amount);
    }

    private string FormatScore(int value)
    {
        return current.scoreOutOf > 0
            ? LocalizationManager.Get("result_level_score", value, current.scoreOutOf)
            : value.ToString();
    }

    private Color HeadlineColor(RunResult.Mood mood)
    {
        switch (mood)
        {
            case RunResult.Mood.NewBest:
            case RunResult.Mood.First:
            case RunResult.Mood.Near:
                return celebrateColor;
            case RunResult.Mood.Complete:
                return honoredColor;
            case RunResult.Mood.Broken:
                return brokenColor;
            default:
                return neutralColor;
        }
    }

    private static void SetText(TextMeshProUGUI label, string text, Color color)
    {
        if (label == null) return;
        label.text = text;
        label.color = color;
    }

    private static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
}
