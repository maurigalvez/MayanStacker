using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Ritual of The Sun briefing: the gate between loading the Daily and the first drop.
/// Reads top to bottom as the three things a player needs before committing to today's run:
///
///   1. THE GOAL      — how to complete the ritual (stones to place, and what breaks it);
///   2. TODAY'S OMENS — every temple rule, modifier and granted power in play, one line each;
///   3. YOUR STANDING — today's rank and score, or how to get onto the board;
///
/// then PLAY, the only way into the run. It opens on the main menu, before the game scene
/// loads (Back simply closes it); the game scene only shows it itself when it was reached
/// without the menu (Back then returns to the menu).
///
/// Authored prefab only (Resources/UI/RitualBriefing, generated in the house style by
/// TamalStacker ▸ Daily Challenge ▸ Create Ritual Briefing Prefab). Without it
/// <see cref="TryShow"/> returns false and the scene's older briefing panel is used instead.
/// All copy is filled from localization at runtime; text in the prefab is layout preview.
/// </summary>
public class RitualBriefingView : MonoBehaviour
{
    public const string PrefabResourcePath = "UI/RitualBriefing";

    /// <summary>Over the HUD and its overlays, under the tutorial and language screens.</summary>
    public const int SortingOrder = 4400;

    private const string LeaderboardName = "DailyChallenge_Leaderboard";

    [Header("Frame")]
    [SerializeField] private Image backdrop;
    [SerializeField] private RectTransform modal;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI eyebrowLabel;
    [SerializeField] private TextMeshProUGUI dateLabel;
    [SerializeField] private TextMeshProUGUI timeLeftLabel;
    [SerializeField] private TextMeshProUGUI ritualNameLabel;
    [SerializeField] private TextMeshProUGUI taglineLabel;
    [Tooltip("Suns showing the ritual's difficulty, left to right. Optional.")]
    [SerializeField] private Image[] difficultyPips = new Image[0];
    [SerializeField] private Color pipOnColor = new Color(0.98f, 0.78f, 0.3f, 1f);
    [SerializeField] private Color pipOffColor = new Color(1f, 1f, 1f, 0.18f);

    [Header("1 · The goal")]
    [SerializeField] private TextMeshProUGUI goalHeaderLabel;
    [SerializeField] private TextMeshProUGUI goalLabel;
    [SerializeField] private TextMeshProUGUI goalNoteLabel;

    [Header("2 · Today's omens")]
    [SerializeField] private TextMeshProUGUI omensHeaderLabel;
    [Tooltip("Parent the rows are added to (a vertical layout).")]
    [SerializeField] private RectTransform omenList;
    [Tooltip("Row cloned once per omen. Kept inactive at runtime.")]
    [SerializeField] private RitualOmenRow omenTemplate;
    [SerializeField] private Color ruleColor = new Color(0.33f, 0.88f, 0.7f, 1f);
    [SerializeField] private Color modifierColor = new Color(0.98f, 0.78f, 0.3f, 1f);

    [Header("3 · Your standing")]
    [SerializeField] private TextMeshProUGUI standingHeaderLabel;
    [SerializeField] private TextMeshProUGUI standingLabel;
    [SerializeField] private TextMeshProUGUI standingNoteLabel;
    [SerializeField] private Color rankedColor = new Color(0.98f, 0.78f, 0.3f, 1f);
    [SerializeField] private Color unrankedColor = new Color(0.96f, 0.9f, 0.75f, 1f);

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private TextMeshProUGUI playLabel;
    [Tooltip("Optional: back to the main menu.")]
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI backLabel;

    private readonly List<RitualOmenRow> rows = new List<RitualOmenRow>();
    private Action onPlay;
    private Action onBack;
    private static RitualBriefingView open;

    /// <summary>True while a briefing is on screen.</summary>
    public static bool IsOpen => open != null;
    private UIManager uiManager;
    private DailyChallengeConfig config;
    private bool closing;
    private bool ticking;

    public bool IsUsable =>
        backdrop != null && modal != null && ritualNameLabel != null && goalLabel != null &&
        omenList != null && omenTemplate != null && omenTemplate.IsUsable &&
        standingLabel != null && playButton != null;

    /// <summary>
    /// Shows the briefing for <paramref name="cfg"/>; <paramref name="play"/> runs after PLAY
    /// (it closes first), <paramref name="back"/> after Back (null = just close).
    /// <paramref name="ui"/> is the game scene's UIManager, or null on the main menu.
    /// Returns false (and shows nothing) when the prefab is missing or unusable, so the
    /// caller can fall back to the old flow.
    /// </summary>
    public static bool TryShow(DailyChallengeConfig cfg, Action play, UIManager ui, Action back = null)
    {
        if (open != null) return true;

        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[RitualBriefing] Resources/{PrefabResourcePath}.prefab is missing - using the scene's " +
                             "briefing panel. Create it with TamalStacker ▸ Daily Challenge ▸ Create Ritual Briefing Prefab.");
            return false;
        }

        GameObject go = Instantiate(prefab);
        go.name = "RitualBriefing";
        var view = go.GetComponent<RitualBriefingView>();
        if (view == null || !view.IsUsable)
        {
            Debug.LogWarning($"[RitualBriefing] Resources/{PrefabResourcePath} has no usable RitualBriefingView " +
                             "(needs Backdrop, Modal, Ritual Name, Goal, Omen List + Row Template, Standing and PLAY).");
            go.SetActive(false);
            Destroy(go);
            return false;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = SortingOrder;

        view.Open(cfg, play, ui, back);
        return true;
    }

    private void Open(DailyChallengeConfig cfg, Action play, UIManager ui, Action back)
    {
        open = this;
        config = cfg;
        onPlay = play;
        onBack = back;
        uiManager = ui;

        if (uiManager != null) uiManager.BeginExternalDailyBriefing();

        omenTemplate.gameObject.SetActive(false);
        playButton.onClick.AddListener(OnPlay);
        if (backButton != null) backButton.onClick.AddListener(OnBack);

        FillHeader();
        FillGoal();
        FillOmens();
        FillStandingLoading();

        UIPopup.Show(backdrop.gameObject);
        UIPopup.Show(modal.gameObject);

        BackButton.Register(OnHardwareBack);
        StartCoroutine(Countdown());
        StartCoroutine(FetchStanding());

        GameAnalytics.Track("daily_briefing_shown", AnalyticsData());
    }

    // ---- Content ----

    private void FillHeader()
    {
        DailyRitual ritual = config.ritual;

        SetText(eyebrowLabel, LocalizationManager.Get("daily_challenge_title"));
        SetText(dateLabel, DailyChallengeManager.TodaysDateLabelUtc());
        ritualNameLabel.text = DailyChallengeManager.DisplayNameFor(config);
        SetText(taglineLabel, ritual != null && !string.IsNullOrEmpty(ritual.taglineKey)
            ? LocalizationManager.Get(ritual.taglineKey)
            : LocalizationManager.Get("daily_challenge_subtitle"));

        int difficulty = ritual != null ? ritual.difficulty : 0;
        for (int i = 0; i < difficultyPips.Length; i++)
        {
            if (difficultyPips[i] == null) continue;
            difficultyPips[i].gameObject.SetActive(difficulty > 0);
            difficultyPips[i].color = i < difficulty ? pipOnColor : pipOffColor;
        }

        SetText(goalHeaderLabel, LocalizationManager.Get("daily_briefing_goal_header"));
        SetText(omensHeaderLabel, LocalizationManager.Get("daily_briefing_omens_header"));
        SetText(standingHeaderLabel, LocalizationManager.Get("daily_briefing_standing_header"));
        SetText(playLabel, LocalizationManager.Get("daily_briefing_play"));
        SetText(backLabel, LocalizationManager.Get("daily_briefing_back"));
    }

    private void FillGoal()
    {
        goalLabel.text = LocalizationManager.Get("daily_goal_stack", config.blockCount);

        // What can break the ritual, else what the score rewards. One line: the omens below
        // carry the details.
        RunModifierDefinition rules = CombinedRules();
        string note;
        if (rules.endRunOnPoorLanding) note = LocalizationManager.Get("daily_goal_note_fragile");
        else if (HasModifier(RunModifier.SpeedRun)) note = LocalizationManager.Get("daily_goal_note_speed");
        else if (config.ritual != null && config.ritual.HasAnyRule) note = LocalizationManager.Get("daily_goal_note_rules");
        else note = LocalizationManager.Get("daily_goal_note_score");
        SetText(goalNoteLabel, note);
    }

    private void FillOmens()
    {
        DailyRitual ritual = config.ritual;

        if (ritual == null)
        {
            // The original Daily: its one modifier is the only omen.
            AddModifierRow(RunModifierDefinition.FromDaily(config.modifier), null);
            return;
        }

        AddRuleRow(ritual.rule);
        if (ritual.secondRule != ritual.rule) AddRuleRow(ritual.secondRule);

        if (ritual.modifiers != null)
        {
            var seen = new HashSet<RunModifier>();
            foreach (RunModifier m in ritual.modifiers)
            {
                if (m == RunModifier.None || !seen.Add(m)) continue;
                AddModifierRow(m, ritual.tuning);
            }
        }

        if (ritual.grantsPower && !ritual.HasModifier(RunModifier.GiftOfTheGods))
        {
            PowerSettings powers = PowerSettings.Current;
            PowerDefinition def = powers.Get(ritual.grantedPower);
            AddRow(LocalizationManager.Get("daily_omen_power", LocalizationManager.Get(def.nameKey)),
                powers.Describe(ritual.grantedPower), def.icon, def.accentColor);
        }

        if (rows.Count == 0)
        {
            AddRow(LocalizationManager.Get("daily_omen_none"), LocalizationManager.Get("daily_omen_none_desc"),
                null, modifierColor);
        }
    }

    private void AddRuleRow(LevelRule rule)
    {
        if (rule == LevelRule.None) return;
        // Same badge the temple pre-play screen shows (TempleRuleIcons).
        AddRow(LocalizationManager.Get(TempleRules.NameKey(rule)),
            LocalizationManager.Get(TempleRules.ShortKey(rule)),
            TempleRuleIcons.For(rule), ruleColor);
    }

    private void AddModifierRow(RunModifier modifier, RunModifierTuning tuning)
    {
        if (modifier == RunModifier.None) return;

        RunModifierDefinition def = RunModifierDefinition.For(modifier);
        if (tuning != null) tuning.ApplyTo(ref def);

        // {0} is the one number a player needs: Hot Stone's swings, Feather Fall's weight.
        object arg = null;
        switch (modifier)
        {
            case RunModifier.HotStone: arg = def.autoDropSwings.ToString("0.#"); break;
            case RunModifier.ShrinkingOfferings: arg = Mathf.RoundToInt(def.minWidthScale * 100f); break;
        }

        string description = arg != null
            ? LocalizationManager.Get(def.descriptionKey, arg)
            : LocalizationManager.Get(def.descriptionKey);

        AddRow(LocalizationManager.Get(def.nameKey), description, ModifierIcon(modifier), modifierColor);
    }

    /// <summary>
    /// Resources/UI/Modifiers/Mod_&lt;RunModifier&gt;_Icon, like the rule and power icons, so adding
    /// art is dropping a file in. Until then the row simply has no icon.
    /// </summary>
    public static string ModifierIconResourcePath(RunModifier m) => "UI/Modifiers/Mod_" + m + "_Icon";

    private static readonly Dictionary<RunModifier, Sprite> modifierIcons = new Dictionary<RunModifier, Sprite>();

    private static Sprite ModifierIcon(RunModifier m)
    {
        if (!modifierIcons.TryGetValue(m, out Sprite sprite))
        {
            sprite = Resources.Load<Sprite>(ModifierIconResourcePath(m));
            modifierIcons[m] = sprite;
        }
        return sprite;
    }

    private void AddRow(string title, string description, Sprite icon, Color accent)
    {
        RitualOmenRow row = Instantiate(omenTemplate, omenList, false);
        row.gameObject.SetActive(true);
        row.name = "Omen_" + rows.Count;
        row.Set(title, description, icon, accent);
        rows.Add(row);
    }

    private RunModifierDefinition CombinedRules()
    {
        if (config.ritual == null) return RunModifierDefinition.For(RunModifierDefinition.FromDaily(config.modifier));

        RunModifierDefinition combined = RunModifierDefinition.For(RunModifier.None);
        if (config.ritual.modifiers != null)
        {
            foreach (RunModifier m in config.ritual.modifiers)
                combined = RunModifierDefinition.Combine(combined, RunModifierDefinition.For(m));
        }
        return combined;
    }

    private bool HasModifier(RunModifier m) =>
        config.ritual != null
            ? config.ritual.HasModifier(m)
            : RunModifierDefinition.FromDaily(config.modifier) == m;

    // ---- Standing ----

    private void FillStandingLoading()
    {
        standingLabel.text = LocalizationManager.Get("daily_standing_loading");
        standingLabel.color = unrankedColor;
        SetText(standingNoteLabel, StreakLine());
    }

    private IEnumerator FetchStanding()
    {
        // Let a login that's finishing in the background land before deciding we're offline.
        var playFab = DependencyRegistry.Find<PlayFabManager>();
        float waitUntil = Time.realtimeSinceStartup + 3f;
        while (playFab != null && !playFab.IsLoggedIn && !NetworkUtility.IsOffline()
               && Time.realtimeSinceStartup < waitUntil)
        {
            yield return new WaitForSecondsRealtime(0.25f);
        }

        if (playFab == null || !playFab.IsLoggedIn || NetworkUtility.IsOffline())
        {
            ShowOfflineStanding();
            yield break;
        }

        playFab.GetPlayerLeaderboardPosition(LeaderboardName, 1,
            entries =>
            {
                if (this == null) return;
                LeaderboardEntry me = entries != null ? entries.Find(e => e.isCurrentPlayer) : null;
                if (me != null && me.score > 0) ShowRanked(me.position, me.score);
                else ShowUnranked();
            },
            error =>
            {
                if (this == null) return;
                ShowOfflineStanding();
            });
    }

    private void ShowRanked(int position, int score)
    {
        standingLabel.text = LocalizationManager.Get("daily_standing_rank", position);
        standingLabel.color = rankedColor;
        SetText(standingNoteLabel, JoinLines(LocalizationManager.Get("daily_standing_score", score), StreakLine()));
    }

    private void ShowUnranked()
    {
        standingLabel.text = LocalizationManager.Get("daily_standing_unranked");
        standingLabel.color = unrankedColor;
        SetText(standingNoteLabel, JoinLines(LocalizationManager.Get("daily_standing_unranked_note"), StreakLine()));
    }

    /// <summary>No rank to read: show today's local best when there is one, else how to enter.</summary>
    private void ShowOfflineStanding()
    {
        int best = DailyChallengeManager.TodaysBestScore();
        if (best <= 0)
        {
            ShowUnranked();
            return;
        }

        standingLabel.text = LocalizationManager.Get("daily_standing_score", best);
        standingLabel.color = rankedColor;
        SetText(standingNoteLabel, JoinLines(LocalizationManager.Get("daily_standing_offline"), StreakLine()));
    }

    private static string StreakLine()
    {
        int streak = DailyStreak.Current;
        return streak > 0 ? LocalizationManager.GetPlural("daily_streak_count", streak, streak) : string.Empty;
    }

    private static string JoinLines(string a, string b)
    {
        if (string.IsNullOrEmpty(b)) return a;
        if (string.IsNullOrEmpty(a)) return b;
        return a + "  ·  " + b;
    }

    // ---- Countdown ----

    private IEnumerator Countdown()
    {
        ticking = true;
        while (ticking && timeLeftLabel != null)
        {
            string clock = DailyChallengeManager.FormatCountdown(DailyChallengeManager.TimeUntilNextResetUtc());
            timeLeftLabel.text = LocalizationManager.Get("daily_time_left", clock);
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    // ---- Buttons ----

    private void OnPlay()
    {
        if (closing) return;
        closing = true;
        GameAnalytics.Track("daily_briefing_play", AnalyticsData());

        Close(() =>
        {
            if (uiManager != null) uiManager.EndExternalDailyBriefing();
            Action play = onPlay;
            onPlay = null;
            play?.Invoke();
        });
    }

    private void OnBack()
    {
        if (closing) return;
        closing = true;
        GameAnalytics.Track("daily_briefing_back", AnalyticsData());
        Close(onBack);
    }

    private bool OnHardwareBack()
    {
        OnBack();
        return true;
    }

    private void Close(Action after)
    {
        ticking = false;
        BackButton.Unregister(OnHardwareBack);
        playButton.interactable = false;
        if (backButton != null) backButton.interactable = false;

        UIPopup.Hide(backdrop.gameObject);
        UIPopup.Hide(modal.gameObject, () =>
        {
            after?.Invoke();
            if (this != null) Destroy(gameObject);
        });
    }

    private void OnDestroy()
    {
        if (open == this) open = null;
        BackButton.Unregister(OnHardwareBack);
    }

    private Dictionary<string, object> AnalyticsData()
    {
        return new Dictionary<string, object>
        {
            { "ritual", config.ritual != null ? config.ritual.id : config.modifier.ToString() },
            { "day", config.dayNumberUtc }
        };
    }

    private static void SetText(TextMeshProUGUI label, string text)
    {
        if (label == null) return;
        label.text = text;
        label.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }
}
