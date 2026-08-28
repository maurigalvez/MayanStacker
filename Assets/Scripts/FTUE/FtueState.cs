using UnityEngine;

/// <summary>
/// Persistent first-time-user-experience state, in PlayerPrefs.
///
/// This is the single source of truth for "how new is this player?", read by the tutorial,
/// the ad gate, the main menu's first-launch routing, and the stack's forgiveness rules.
/// Everything is static so any system can ask without needing a scene reference.
/// </summary>
public static class FtueState
{
    private const string PP_INSTALLED = "Ftue_Installed";
    private const string PP_SESSION_COUNT = "Ftue_SessionCount";
    private const string PP_LIFETIME_RUNS = "Ftue_LifetimeRuns";
    private const string PP_TUTORIAL_STATE = "Ftue_TutorialState";
    private const string PP_FIRST_DROP_DONE = "Ftue_FirstDropDone";
    private const string PP_FIRST_TEMPLE_DONE = "Ftue_FirstTempleDone";
    private const string PP_GRACE_RETRY_USED = "Ftue_GraceRetryUsed";
    private const string PP_LANGUAGE_CHOSEN = "Ftue_LanguageChosen";

    /// <summary>Legacy key from the old 3-second instruction overlay, cleared on migration.</summary>
    private const string PP_LEGACY_INSTRUCTIONS_SEEN = "HasSeenInstructions";

    public enum TutorialState
    {
        NotStarted = 0,
        InProgress = 1,
        Completed = 2,
        Skipped = 3
    }

    /// <summary>How many blocks the tutorial forgives before topple rules apply normally.</summary>
    public const int ForgivenBlockCount = 3;

    /// <summary>Runs the player must have finished before interstitials are allowed.</summary>
    public const int AdGraceRunThreshold = 6;

    /// <summary>Sessions the player must have opened before interstitials are allowed.</summary>
    public const int AdGraceSessionThreshold = 2;

    // Session-scoped (not persisted).
    private static int runsThisSession;
    private static bool launchRegistered;

    #region Launch & session

    /// <summary>
    /// Call once per app launch. Fires first_launch/session_start analytics and advances
    /// the session counter. Safe to call more than once — only the first call counts.
    /// </summary>
    public static void RegisterLaunch()
    {
        if (launchRegistered) return;
        launchRegistered = true;

        bool firstEver = PlayerPrefs.GetInt(PP_INSTALLED, 0) == 0;

        if (firstEver)
        {
            PlayerPrefs.SetInt(PP_INSTALLED, 1);
            MigrateLegacyInstructionFlag();
        }

        int session = PlayerPrefs.GetInt(PP_SESSION_COUNT, 0) + 1;
        PlayerPrefs.SetInt(PP_SESSION_COUNT, session);
        PlayerPrefs.Save();

        if (firstEver) GameAnalytics.FirstLaunch();
        GameAnalytics.SessionStart(session);
    }

    /// <summary>
    /// A player who already saw the old timed instruction overlay isn't new — don't put
    /// them through the tutorial just because they updated the app.
    /// </summary>
    private static void MigrateLegacyInstructionFlag()
    {
        if (PlayerPrefs.GetInt(PP_LEGACY_INSTRUCTIONS_SEEN, 0) == 1)
        {
            PlayerPrefs.SetInt(PP_TUTORIAL_STATE, (int)TutorialState.Completed);
            PlayerPrefs.SetInt(PP_FIRST_DROP_DONE, 1);
        }
    }

    /// <summary>True when this launch is the player's very first, before any run.</summary>
    public static bool IsFirstLaunch => SessionNumber <= 1 && LifetimeRuns == 0;

    public static int SessionNumber => PlayerPrefs.GetInt(PP_SESSION_COUNT, 0);

    public static int RunsThisSession => runsThisSession;

    #endregion

    #region Progress signals

    public static int LifetimeRuns => PlayerPrefs.GetInt(PP_LIFETIME_RUNS, 0);

    /// <summary>Call once per run start.</summary>
    public static void RegisterRunStarted()
    {
        runsThisSession++;
        PlayerPrefs.SetInt(PP_LIFETIME_RUNS, LifetimeRuns + 1);
        PlayerPrefs.Save();
    }

    /// <summary>True once the player has completed any temple at least once.</summary>
    public static bool HasCompletedFirstTemple => PlayerPrefs.GetInt(PP_FIRST_TEMPLE_DONE, 0) == 1;

    public static void MarkFirstTempleCompleted()
    {
        if (HasCompletedFirstTemple) return;
        PlayerPrefs.SetInt(PP_FIRST_TEMPLE_DONE, 1);
        PlayerPrefs.Save();
    }

    public static bool HasDroppedFirstBlock => PlayerPrefs.GetInt(PP_FIRST_DROP_DONE, 0) == 1;

    /// <summary>Marks the first-ever drop and fires the analytics event exactly once.</summary>
    public static void MarkFirstDrop()
    {
        if (HasDroppedFirstBlock) return;
        PlayerPrefs.SetInt(PP_FIRST_DROP_DONE, 1);
        PlayerPrefs.Save();
        GameAnalytics.FirstDrop();
    }

    #endregion

    #region Language

    /// <summary>
    /// True once the player has picked a language for themselves. Until then the game is
    /// running on a guess from the device locale, which only covers the six shipped
    /// locales — everyone else was silently defaulted to English.
    /// </summary>
    public static bool HasChosenLanguage => PlayerPrefs.GetInt(PP_LANGUAGE_CHOSEN, 0) == 1;

    public static void MarkLanguageChosen()
    {
        PlayerPrefs.SetInt(PP_LANGUAGE_CHOSEN, 1);
        PlayerPrefs.Save();
    }

    #endregion

    #region Tutorial

    public static TutorialState Tutorial
    {
        get => (TutorialState)PlayerPrefs.GetInt(PP_TUTORIAL_STATE, (int)TutorialState.NotStarted);
        set
        {
            PlayerPrefs.SetInt(PP_TUTORIAL_STATE, (int)value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>True when the tutorial still needs to run.</summary>
    public static bool NeedsTutorial =>
        Tutorial == TutorialState.NotStarted || Tutorial == TutorialState.InProgress;

    /// <summary>
    /// True while the player is inside the protected first experience: the tutorial hasn't
    /// resolved and they haven't finished a temple yet. Drives topple forgiveness.
    /// </summary>
    public static bool IsInFtue => NeedsTutorial || !HasCompletedFirstTemple;

    /// <summary>The one free retry the FTUE grants on a first topple.</summary>
    public static bool GraceRetryAvailable =>
        IsInFtue && PlayerPrefs.GetInt(PP_GRACE_RETRY_USED, 0) == 0;

    public static void ConsumeGraceRetry()
    {
        PlayerPrefs.SetInt(PP_GRACE_RETRY_USED, 1);
        PlayerPrefs.Save();
    }

    #endregion

    #region Gates

    /// <summary>
    /// Whether interstitials are allowed yet. There is no monetization plan to protect
    /// right now, so this errs generous: a new player sees no ads until they have finished
    /// a temple, played a handful of runs, and come back for a second session.
    /// </summary>
    public static bool AdsAllowed =>
        HasCompletedFirstTemple &&
        LifetimeRuns >= AdGraceRunThreshold &&
        SessionNumber >= AdGraceSessionThreshold;

    /// <summary>Short slug describing why ads are still suppressed, for analytics.</summary>
    public static string AdSuppressionReason
    {
        get
        {
            if (!HasCompletedFirstTemple) return "no_temple_yet";
            if (LifetimeRuns < AdGraceRunThreshold) return "run_grace";
            if (SessionNumber < AdGraceSessionThreshold) return "session_grace";
            return "none";
        }
    }

    /// <summary>
    /// Whether returning-player surfaces (the Daily ritual, streaks) should be promoted.
    /// They dilute a first session, so they wait until the FTUE has resolved.
    /// </summary>
    public static bool ShouldPromoteReturningPlayerFeatures =>
        !NeedsTutorial && HasCompletedFirstTemple;

    #endregion

    /// <summary>Wipe all FTUE state. Used by PlayerDataReset for testing.</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(PP_INSTALLED);
        PlayerPrefs.DeleteKey(PP_SESSION_COUNT);
        PlayerPrefs.DeleteKey(PP_LIFETIME_RUNS);
        PlayerPrefs.DeleteKey(PP_TUTORIAL_STATE);
        PlayerPrefs.DeleteKey(PP_FIRST_DROP_DONE);
        PlayerPrefs.DeleteKey(PP_FIRST_TEMPLE_DONE);
        PlayerPrefs.DeleteKey(PP_GRACE_RETRY_USED);
        PlayerPrefs.DeleteKey(PP_LANGUAGE_CHOSEN);
        PlayerPrefs.DeleteKey(PP_LEGACY_INSTRUCTIONS_SEEN);
        PlayerPrefs.Save();

        runsThisSession = 0;
        launchRegistered = false;
    }
}
