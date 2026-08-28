using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thin static funnel-analytics wrapper.
///
/// Events are routed through PlayFab's WritePlayerEvent (no new SDK to integrate), but no
/// call site knows that — the backend can be swapped by editing Flush() alone.
///
/// Events fired before login are queued and flushed once PlayFabManager reports a session,
/// which matters because the two most important events in the funnel (first_launch and the
/// tutorial beats) all happen before authentication finishes.
/// </summary>
public static class GameAnalytics
{
    private struct PendingEvent
    {
        public string Name;
        public Dictionary<string, object> Data;
    }

    // Bounded so a permanently-offline player can't grow this without limit.
    private const int MaxPendingEvents = 128;

    private static readonly Queue<PendingEvent> pending = new Queue<PendingEvent>();
    private static PlayFabManager playFabManager;

    /// <summary>Set false to mute analytics entirely (e.g. from a privacy setting).</summary>
    public static bool Enabled = true;

    /// <summary>Number of events waiting on a login. Exposed for debugging.</summary>
    public static int PendingCount => pending.Count;

    #region Core

    /// <summary>
    /// Record an event. Safe to call from anywhere at any time — never throws, never
    /// blocks, and silently no-ops if analytics is disabled.
    /// </summary>
    public static void Track(string eventName, Dictionary<string, object> data = null)
    {
        if (!Enabled || string.IsNullOrEmpty(eventName)) return;

        var evt = new PendingEvent { Name = eventName, Data = data };

        if (!TrySend(evt))
        {
            if (pending.Count >= MaxPendingEvents)
            {
                // Drop the oldest — recent funnel state is more useful than ancient state.
                pending.Dequeue();
            }
            pending.Enqueue(evt);
        }
    }

    /// <summary>
    /// Attempt to flush everything queued. Called when PlayFab reports a login and
    /// periodically by AnalyticsSession; cheap and safe to call when there's nothing to do.
    /// </summary>
    public static void Flush()
    {
        if (!Enabled || pending.Count == 0) return;
        if (!IsReady()) return;

        // Re-queue anything that fails mid-flush rather than losing it.
        int count = pending.Count;
        for (int i = 0; i < count; i++)
        {
            var evt = pending.Dequeue();
            if (!TrySend(evt))
            {
                pending.Enqueue(evt);
                return;
            }
        }
    }

    private static bool IsReady()
    {
        if (playFabManager == null)
        {
            playFabManager = DependencyRegistry.Find<PlayFabManager>();
        }
        return playFabManager != null && playFabManager.IsLoggedIn;
    }

    private static bool TrySend(PendingEvent evt)
    {
        if (!IsReady()) return false;

        playFabManager.LogAnalyticsEvent(evt.Name, evt.Data);
        return true;
    }

    /// <summary>
    /// Drop queued events without sending. Used when the player resets their data.
    /// </summary>
    public static void ClearPending()
    {
        pending.Clear();
    }

    private static Dictionary<string, object> Data(params object[] keyValuePairs)
    {
        var dict = new Dictionary<string, object>();
        for (int i = 0; i + 1 < keyValuePairs.Length; i += 2)
        {
            string key = keyValuePairs[i] as string;
            if (!string.IsNullOrEmpty(key)) dict[key] = keyValuePairs[i + 1];
        }
        return dict;
    }

    #endregion

    #region Funnel events

    /// <summary>Fired exactly once, on the very first launch after install.</summary>
    public static void FirstLaunch()
    {
        Track("first_launch", Data(
            "platform", Application.platform.ToString(),
            "version", Application.version,
            "locale", Application.systemLanguage.ToString()));
    }

    /// <summary>Fired when a session begins (first launch included).</summary>
    public static void SessionStart(int sessionNumber)
    {
        Track("session_start", Data("session_number", sessionNumber));
    }

    /// <summary>A tutorial beat was reached. Beats are 1-indexed.</summary>
    public static void TutorialStep(int beat)
    {
        Track("tutorial_step", Data("beat", beat));
    }

    /// <summary>The player skipped out of the tutorial at the given beat.</summary>
    public static void TutorialSkipped(int beat)
    {
        Track("tutorial_skipped", Data("beat", beat));
    }

    /// <summary>The player completed all tutorial beats.</summary>
    public static void TutorialCompleted()
    {
        Track("tutorial_completed");
    }

    /// <summary>The player's very first block drop, ever.</summary>
    public static void FirstDrop()
    {
        Track("first_drop");
    }

    /// <summary>A run started. Level is -1 for non-level modes.</summary>
    public static void RunStart(GameMode mode, int levelNumber, int lifetimeRuns)
    {
        Track("run_start", Data(
            "mode", mode.ToString(),
            "level", levelNumber,
            "lifetime_runs", lifetimeRuns));
    }

    /// <summary>
    /// A run ended. <paramref name="cause"/> is a short slug: "topple", "fragile",
    /// "daily_target", "level_complete", "quit".
    /// </summary>
    public static void RunEnd(GameMode mode, int levelNumber, int blocks, int score, string cause)
    {
        Track("run_end", Data(
            "mode", mode.ToString(),
            "level", levelNumber,
            "blocks", blocks,
            "score", score,
            "cause", cause));
    }

    /// <summary>A level was completed. FirstCompletion separates progression from replay.</summary>
    public static void LevelComplete(int levelNumber, int stars, int score, bool firstCompletion)
    {
        Track("level_complete", Data(
            "level", levelNumber,
            "stars", stars,
            "score", score,
            "first_completion", firstCompletion));
    }

    /// <summary>An interstitial was actually shown.</summary>
    public static void AdShown(string placement, int lifetimeRuns)
    {
        Track("ad_shown", Data(
            "placement", placement,
            "lifetime_runs", lifetimeRuns));
    }

    /// <summary>An interstitial was suppressed by the FTUE grace period.</summary>
    public static void AdSuppressed(string reason, int lifetimeRuns)
    {
        Track("ad_suppressed", Data(
            "reason", reason,
            "lifetime_runs", lifetimeRuns));
    }

    /// <summary>A Daily Challenge run finished, with the streak state it produced.</summary>
    public static void DailyRunComplete(bool ritualComplete, int score, int streak)
    {
        Track("daily_run_complete", Data(
            "ritual_complete", ritualComplete,
            "score", score,
            "streak", streak));
    }

    /// <summary>Local notification permission outcome.</summary>
    public static void NotificationPermission(bool granted)
    {
        Track("notification_permission", Data("granted", granted));
    }

    /// <summary>Session ended (app paused or quit).</summary>
    public static void SessionEnd(float durationSeconds, int runsThisSession)
    {
        Track("session_end", Data(
            "duration_seconds", Mathf.RoundToInt(durationSeconds),
            "runs", runsThisSession));
    }

    #endregion
}
