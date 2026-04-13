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

    [Header("Speed Run modifier tuning")]
    [Tooltip("Multiplier applied to the spawner's swing speed when the SpeedRun modifier is active.")]
    [SerializeField] private float speedRunSwingMultiplier = 1.6f;

    private DailyChallengeFallbackSettings fallbackSettings;
    private DailyChallengeConfig? cachedConfig;
    private bool isActive;
    private int blocksPlaced;
    private float originalSwingSpeedBeforeApply = -1f;

    public bool IsActive => isActive;
    public DailyChallengeConfig CurrentConfig => cachedConfig.GetValueOrDefault();
    public bool HasConfig => cachedConfig.HasValue;
    public int BlocksPlaced => blocksPlaced;
    public int BlockCountTarget => cachedConfig.HasValue ? cachedConfig.Value.blockCount : 0;

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
    }

    /// <summary>
    /// Called by GameManager after a block is scored. Returns true when the run-length cap is reached.
    /// </summary>
    public bool RegisterBlockPlacedAndCheckComplete()
    {
        if (!isActive) return false;
        blocksPlaced++;
        return blocksPlaced >= BlockCountTarget;
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
        int dayNumberUtc = overrideDayNumber ?? ToDayNumberUtc(DateTime.UtcNow);
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
}
