using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

/// <summary>
/// Coordinates the Daily Challenge mode:
///   - Resolves "today's" config (modifier + block count) from PlayFab Title Data,
///     using PlayFab server time so device-clock tampering can't reroll the modifier.
///   - Tracks blocks placed during the run so GameManager can end the run at the cap.
///   - Applies modifier side-effects that touch other systems (e.g. SpawnerHolder swing speed).
/// Other systems read the cached <see cref="CurrentConfig"/> via guarded checks like
/// <c>if (dailyMgr.IsActive &amp;&amp; dailyMgr.CurrentConfig.modifier == DailyChallengeModifier.X)</c>.
/// </summary>
public class DailyChallengeManager : MonoBehaviour
{
    private const string TITLE_KEY_ENABLED = "DailyChallenge_Enabled";
    private const string TITLE_KEY_BLOCK_COUNT = "DailyChallenge_BlockCount";
    private const string TITLE_KEY_ACTIVE_MODIFIERS = "DailyChallenge_ActiveModifiers";
    private const string TITLE_KEY_OVERRIDE = "DailyChallenge_Override";

    private const string FALLBACK_RESOURCE_PATH = "DailyChallenge/DailyChallengeFallbackSettings";

    // PlayerPrefs keys for local daily state (played-today badge + personal best).
    // Keyed by the day number from NowUtc(), which tracks server time once GetTime lands.
    private const string PP_LAST_PLAYED_DAY = "Daily_LastPlayedDay";
    private const string PP_BEST_PREFIX = "Daily_Best_";

    // Server clock correction. Set from PlayFab GetTime; until then NowUtc() falls back to
    // the device clock. Everything day- or countdown-related routes through NowUtc() so the
    // displayed date, the "Time Left" countdown and the modifier roll can't disagree.
    private static TimeSpan serverTimeOffset = TimeSpan.Zero;
    private static bool hasServerTime = false;

#if UNITY_EDITOR
    [Header("Editor Test Override")]
    [Tooltip("Editor-only: when enabled, forces the chosen modifier and skips PlayFab. Has no effect in builds.")]
    [SerializeField] private bool editorForceModifier = false;
    [SerializeField] private DailyChallengeModifier editorForcedModifier = DailyChallengeModifier.SpeedRun;
    [Tooltip("Editor-only: block count to use when the override is active.")]
    [SerializeField] private int editorForcedBlockCount = 30;
#endif

    [Header("Speed Run modifier tuning")]
    [Tooltip("Multiplier applied to the spawner's swing speed when the SpeedRun modifier is active.")]
    [SerializeField] private float speedRunSwingMultiplier = 1.6f;
    [Tooltip("Time limit in seconds for full bonus in SpeedRun mode. Finishing at or above this awards zero bonus.")]
    [SerializeField] private float speedRunTimeLimitSeconds = 120f;
    [Tooltip("Maximum bonus points awarded for finishing SpeedRun instantly (scales linearly with remaining time).")]
    [SerializeField] private int speedRunMaxTimeBonus = 5000;

    private DailyChallengeFallbackSettings fallbackSettings;
    private DailyChallengeConfig? cachedConfig;
    private bool isActive;
    private int blocksPlaced;
    private bool runCompleted;
    private float originalSwingSpeedBeforeApply = -1f;
    private float runStartTime;

    public bool IsActive => isActive;
    public DailyChallengeConfig CurrentConfig => cachedConfig.GetValueOrDefault();
    public bool HasConfig => cachedConfig.HasValue;
    public int BlocksPlaced => blocksPlaced;
    /// <summary>True once the run reached its block-count cap (a completed ritual). False after an early topple/fragile break.</summary>
    public bool RunCompleted => runCompleted;
    public int BlockCountTarget => cachedConfig.HasValue ? cachedConfig.Value.blockCount : 0;
    public float ElapsedTime => isActive ? Time.time - runStartTime : 0f;
    public float SpeedRunTimeLimit => speedRunTimeLimitSeconds;
    public bool IsSpeedRun => isActive && cachedConfig.HasValue && cachedConfig.Value.modifier == DailyChallengeModifier.SpeedRun;

    private void Awake()
    {
        DependencyRegistry.Register<DailyChallengeManager>(this);
        fallbackSettings = Resources.Load<DailyChallengeFallbackSettings>(FALLBACK_RESOURCE_PATH);
        if (fallbackSettings == null)
        {
            Debug.LogWarning($"[DailyChallenge] Fallback settings not found at Resources/{FALLBACK_RESOURCE_PATH}. Using hardcoded defaults.");
        }
    }

    private void OnDestroy()
    {
        DependencyRegistry.Unregister<DailyChallengeManager>(this);
    }

    /// <summary>
    /// Fetch today's Daily Challenge config from PlayFab. Falls back to local data if PlayFab is unavailable.
    /// Once resolved the config is cached for the rest of the session.
    /// </summary>
    public void FetchTodaysConfig(Action<DailyChallengeConfig> onReady)
    {
        if (cachedConfig.HasValue)
        {
            onReady?.Invoke(cachedConfig.Value);
            return;
        }

#if UNITY_EDITOR
        if (editorForceModifier)
        {
            int dayNumberUtc = ToDayNumberUtc(NowUtc());
            int blocks = editorForcedBlockCount > 0 ? editorForcedBlockCount : 30;
            var forced = new DailyChallengeConfig
            {
                modifier = editorForcedModifier,
                blockCount = blocks,
                dayNumberUtc = dayNumberUtc
            };
            cachedConfig = forced;
            Debug.Log($"[DailyChallenge] EDITOR OVERRIDE active: modifier={forced.modifier}, blockCount={forced.blockCount}");
            onReady?.Invoke(forced);
            return;
        }
#endif

        var playFabManager = DependencyRegistry.Find<PlayFabManager>();
        bool canQueryPlayFab = playFabManager != null && playFabManager.IsLoggedIn && !NetworkUtility.IsOffline();

        if (!canQueryPlayFab)
        {
            Debug.LogWarning("[DailyChallenge] PlayFab unavailable, using local fallback config.");
            DeliverFallback(onReady);
            return;
        }

        // Step 1: server time → defeats local clock tampering.
        PlayFabClientAPI.GetTime(new GetTimeRequest(),
            timeResult =>
            {
                AdoptServerTime(timeResult.Time);
                int dayNumberUtc = ToDayNumberUtc(timeResult.Time);

                // Step 2: pull title data keys.
                var titleDataRequest = new GetTitleDataRequest
                {
                    Keys = new List<string>
                    {
                        TITLE_KEY_ENABLED,
                        TITLE_KEY_BLOCK_COUNT,
                        TITLE_KEY_ACTIVE_MODIFIERS,
                        TITLE_KEY_OVERRIDE
                    }
                };
                PlayFabClientAPI.GetTitleData(titleDataRequest,
                    titleResult =>
                    {
                        var config = ResolveConfigFromTitleData(titleResult.Data, dayNumberUtc);
                        cachedConfig = config;
                        Debug.Log($"[DailyChallenge] Resolved config: modifier={config.modifier}, blockCount={config.blockCount}, day={config.dayNumberUtc}");
                        onReady?.Invoke(config);
                    },
                    error =>
                    {
                        Debug.LogWarning($"[DailyChallenge] GetTitleData failed: {error.GenerateErrorReport()}. Using fallback.");
                        DeliverFallback(onReady, dayNumberUtc);
                    });
            },
            error =>
            {
                Debug.LogWarning($"[DailyChallenge] GetTime failed: {error.GenerateErrorReport()}. Using fallback.");
                DeliverFallback(onReady);
            });
    }

    /// <summary>
    /// Apply the modifier's external side-effects (currently: SpawnerHolder swing speed for SpeedRun).
    /// Combo Chain and Fragile Stack are gated inline by their owning systems via IsActive checks.
    /// </summary>
    public void ApplyModifier(DailyChallengeConfig config)
    {
        cachedConfig = config;
        isActive = true;
        blocksPlaced = 0;
        runCompleted = false;
        runStartTime = Time.time;

        if (config.modifier == DailyChallengeModifier.SpeedRun)
        {
            var spawnerHolder = DependencyRegistry.Find<SpawnerHolder>();
            if (spawnerHolder != null)
            {
                originalSwingSpeedBeforeApply = spawnerHolder.SwingSpeed;
                spawnerHolder.SetSwingSpeed(originalSwingSpeedBeforeApply * speedRunSwingMultiplier);
                Debug.Log($"[DailyChallenge] SpeedRun applied: swing speed {originalSwingSpeedBeforeApply} → {spawnerHolder.SwingSpeed}");
            }
            else
            {
                Debug.LogWarning("[DailyChallenge] SpeedRun: SpawnerHolder not found.");
            }
        }
    }

    /// <summary>
    /// Reset the per-run block counter without touching the modifier.
    /// Called by GameManager.StartGame for the daily mode so restarts work.
    /// </summary>
    public void ResetRunCounter()
    {
        blocksPlaced = 0;
        runCompleted = false;
        runStartTime = Time.time;
    }

    /// <summary>
    /// Called by GameManager after a block is scored. Returns true when the run-length cap is reached.
    /// </summary>
    public bool RegisterBlockPlacedAndCheckComplete()
    {
        if (!isActive) return false;
        blocksPlaced++;
        if (blocksPlaced >= BlockCountTarget)
        {
            runCompleted = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Calculates the time bonus for a completed SpeedRun.
    /// Returns 0 for non-SpeedRun modifiers or if elapsed time exceeds the limit.
    /// </summary>
    public int CalculateSpeedRunTimeBonus()
    {
        if (!IsSpeedRun) return 0;

        float elapsed = ElapsedTime;
        float remaining = speedRunTimeLimitSeconds - elapsed;
        if (remaining <= 0f) return 0;

        float fraction = remaining / speedRunTimeLimitSeconds;
        return Mathf.RoundToInt(fraction * speedRunMaxTimeBonus);
    }

    /// <summary>
    /// Reset session state. Called when leaving the Daily Challenge mode or restarting the run.
    /// Restores any side-effects ApplyModifier touched.
    /// </summary>
    public void EndSession()
    {
        if (!isActive) return;

        if (cachedConfig.HasValue && cachedConfig.Value.modifier == DailyChallengeModifier.SpeedRun
            && originalSwingSpeedBeforeApply > 0f)
        {
            var spawnerHolder = DependencyRegistry.Find<SpawnerHolder>();
            if (spawnerHolder != null)
            {
                spawnerHolder.SetSwingSpeed(originalSwingSpeedBeforeApply);
            }
        }

        isActive = false;
        blocksPlaced = 0;
        originalSwingSpeedBeforeApply = -1f;
    }

    /// <summary>
    /// Localization key for a modifier's display name.
    /// Mirror keys exist with "_desc" suffix for description text.
    /// </summary>
    public static string GetModifierDisplayNameKey(DailyChallengeModifier modifier)
    {
        switch (modifier)
        {
            case DailyChallengeModifier.SpeedRun: return "daily_modifier_speedrun";
            case DailyChallengeModifier.FragileStack: return "daily_modifier_fragilestack";
            case DailyChallengeModifier.ComboChain: return "daily_modifier_combochain";
            default: return "daily_modifier_speedrun";
        }
    }

    public static string GetModifierDescriptionKey(DailyChallengeModifier modifier)
    {
        return GetModifierDisplayNameKey(modifier) + "_desc";
    }

    // ─────────────────────────────────────────────────────────────────────
    // Internals
    // ─────────────────────────────────────────────────────────────────────

    private DailyChallengeConfig ResolveConfigFromTitleData(Dictionary<string, string> data, int dayNumberUtc)
    {
        // Default values if any key is missing.
        int blockCount = fallbackSettings != null ? fallbackSettings.defaultBlockCount : 30;
        DailyChallengeModifier[] modifiers = fallbackSettings != null && fallbackSettings.defaultModifiers != null && fallbackSettings.defaultModifiers.Length > 0
            ? fallbackSettings.defaultModifiers
            : new[] { DailyChallengeModifier.SpeedRun, DailyChallengeModifier.FragileStack, DailyChallengeModifier.ComboChain };

        if (data == null) data = new Dictionary<string, string>();

        if (data.TryGetValue(TITLE_KEY_BLOCK_COUNT, out string blockCountStr)
            && int.TryParse(blockCountStr, out int parsedBlockCount) && parsedBlockCount > 0)
        {
            blockCount = parsedBlockCount;
        }

        if (data.TryGetValue(TITLE_KEY_ACTIVE_MODIFIERS, out string activeModsStr)
            && !string.IsNullOrWhiteSpace(activeModsStr))
        {
            var parsed = ParseModifierList(activeModsStr);
            if (parsed.Count > 0) modifiers = parsed.ToArray();
        }

        // Override wins.
        DailyChallengeModifier resolved;
        if (data.TryGetValue(TITLE_KEY_OVERRIDE, out string overrideStr)
            && !string.IsNullOrWhiteSpace(overrideStr)
            && TryParseModifier(overrideStr.Trim(), out var overrideMod))
        {
            resolved = overrideMod;
        }
        else
        {
            int idx = ((dayNumberUtc % modifiers.Length) + modifiers.Length) % modifiers.Length;
            resolved = modifiers[idx];
        }

        return new DailyChallengeConfig
        {
            modifier = resolved,
            blockCount = blockCount,
            dayNumberUtc = dayNumberUtc
        };
    }

    private void DeliverFallback(Action<DailyChallengeConfig> onReady, int? overrideDayNumber = null)
    {
        int dayNumberUtc = overrideDayNumber ?? ToDayNumberUtc(NowUtc());
        var config = ResolveConfigFromTitleData(null, dayNumberUtc);
        cachedConfig = config;
        onReady?.Invoke(config);
    }

    private static List<DailyChallengeModifier> ParseModifierList(string csv)
    {
        var result = new List<DailyChallengeModifier>();
        string[] parts = csv.Split(',');
        foreach (var raw in parts)
        {
            string trimmed = raw.Trim();
            if (trimmed.Length == 0) continue;
            if (TryParseModifier(trimmed, out var mod))
            {
                result.Add(mod);
            }
            else
            {
                Debug.LogWarning($"[DailyChallenge] Unknown modifier id in title data: '{trimmed}' (skipped)");
            }
        }
        return result;
    }

    private static bool TryParseModifier(string id, out DailyChallengeModifier modifier)
    {
        return Enum.TryParse(id, ignoreCase: true, out modifier);
    }

    private static int ToDayNumberUtc(DateTime utc)
    {
        // Days since 1970-01-01 UTC. Stable across time zones.
        return (int)(utc.Date - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Local daily state (static so the main menu can read it without a
    // DailyChallengeManager instance, which only lives in the game scene).
    // Display-only: keyed on device UTC, so a clock-changer sees a wrong
    // countdown but can't reroll the (server-timed) modifier.
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Records PlayFab's clock so NowUtc() can correct for a wrong device clock.
    /// Called on every successful GetTime; the offset survives scene loads (static).
    /// </summary>
    private static void AdoptServerTime(DateTime serverUtc)
    {
        serverTimeOffset = serverUtc - DateTime.UtcNow;
        hasServerTime = true;

        if (Math.Abs(serverTimeOffset.TotalMinutes) >= 1.0)
        {
            Debug.LogWarning($"[DailyChallenge] Device clock is off by {serverTimeOffset.TotalMinutes:F1} min vs server; correcting date + countdown.");
        }
    }

    /// <summary>
    /// Current UTC time, corrected to PlayFab's clock once GetTime has landed this session.
    /// Falls back to the device clock offline or before the first fetch.
    /// </summary>
    public static DateTime NowUtc()
    {
        return DateTime.UtcNow + serverTimeOffset;
    }

    /// <summary>
    /// Pulls server time on its own, without resolving a full config. Lets surfaces that show the
    /// date/countdown outside the Daily scene (e.g. the main menu, which has no DailyChallengeManager
    /// instance) be server-accurate. Cheap and idempotent — no-ops once the offset is known.
    /// </summary>
    public static void SyncServerTime(Action onComplete = null)
    {
        if (hasServerTime)
        {
            onComplete?.Invoke();
            return;
        }

        if (!PlayFabClientAPI.IsClientLoggedIn() || NetworkUtility.IsOffline())
        {
            onComplete?.Invoke();
            return;
        }

        PlayFabClientAPI.GetTime(new GetTimeRequest(),
            result =>
            {
                AdoptServerTime(result.Time);
                onComplete?.Invoke();
            },
            error =>
            {
                // Non-fatal: the countdown keeps using the device clock.
                Debug.LogWarning($"[DailyChallenge] SyncServerTime failed: {error.GenerateErrorReport()}. Countdown will use the device clock.");
                onComplete?.Invoke();
            });
    }

    /// <summary>True once a PlayFab GetTime has landed, so the countdown is server-accurate.</summary>
    public static bool HasServerTime => hasServerTime;

    /// <summary>UTC day number (days since 1970-01-01), used for local played/best keys.</summary>
    public static int CurrentDayNumberUtc()
    {
        return ToDayNumberUtc(NowUtc());
    }

    /// <summary>Time remaining until the next UTC midnight (when the daily rolls over).</summary>
    public static TimeSpan TimeUntilNextResetUtc()
    {
        DateTime now = NowUtc();
        DateTime nextMidnight = now.Date.AddDays(1);
        return nextMidnight - now;
    }

    /// <summary>
    /// "HH:MM:SS" for a countdown, clamped to [00:00:00, 23:59:59].
    /// The low clamp stops a stale coroutine rendering "-1:59:59" between rollover and the
    /// next refresh; the high clamp covers landing exactly on midnight, where the true
    /// remaining is a full 24h and would otherwise render as "24:00:00".
    /// </summary>
    public static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        int hours = (int)remaining.TotalHours;
        if (hours >= 24) return "23:59:59";

        return $"{hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    /// <summary>
    /// Today's challenge date, localized — e.g. "July 15, 2026" / "15 de julio de 2026".
    /// This is the UTC date on purpose: it's the day the leaderboard and modifier are keyed to,
    /// so players west of UTC will see it flip ahead of their local calendar in the evening.
    /// Month names come from the locale JSON rather than CultureInfo, which is unreliable on IL2CPP.
    /// </summary>
    public static string TodaysDateLabelUtc()
    {
        DateTime today = NowUtc().Date;
        string month = LocalizationManager.Get($"daily_month_{today.Month}");
        return LocalizationManager.Get("daily_date", month, today.Day, today.Year);
    }

    /// <summary>True if a Daily Challenge run has already been recorded for today.</summary>
    public static bool HasPlayedToday()
    {
        return PlayerPrefs.GetInt(PP_LAST_PLAYED_DAY, -1) == CurrentDayNumberUtc();
    }

    /// <summary>The best score recorded locally for today's Daily Challenge (0 if none yet).</summary>
    public static int TodaysBestScore()
    {
        return PlayerPrefs.GetInt(PP_BEST_PREFIX + CurrentDayNumberUtc(), 0);
    }

    /// <summary>
    /// Record a finished run's score, marking "played today" and updating today's best.
    /// Call once per game-over in Daily Challenge mode.
    /// </summary>
    public static void RecordRunResult(int score)
    {
        int day = CurrentDayNumberUtc();
        PlayerPrefs.SetInt(PP_LAST_PLAYED_DAY, day);
        if (score > PlayerPrefs.GetInt(PP_BEST_PREFIX + day, 0))
        {
            PlayerPrefs.SetInt(PP_BEST_PREFIX + day, score);
        }
        PlayerPrefs.Save();
    }
}
