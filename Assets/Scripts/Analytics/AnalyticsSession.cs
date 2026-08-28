using UnityEngine;

/// <summary>
/// Owns the analytics session lifecycle: registers the launch, drains the queued-event
/// backlog once PlayFab authenticates, and reports session duration on pause/quit.
///
/// Self-bootstraps into the first scene loaded and persists, so it needs zero scene wiring
/// (same pattern as GameFeelManager, but this one runs in the menu too).
/// </summary>
public class AnalyticsSession : MonoBehaviour
{
    private const float FlushIntervalSeconds = 10f;

    private static AnalyticsSession instance;

    private float sessionStartTime;
    private float nextFlushTime;
    private bool sessionEndReported;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        var go = new GameObject("AnalyticsSession");
        instance = go.AddComponent<AnalyticsSession>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        sessionStartTime = Time.realtimeSinceStartup;

        // Fires first_launch (once ever) and session_start; both queue until login lands.
        FtueState.RegisterLaunch();

        // Session-start chores that need an always-present owner. This object is the only
        // one guaranteed to exist in every scene, so the app-lifecycle work lands here.
        DailyStreak.RefreshLapsedState();
        NotificationScheduler.RecordPlaySession();

        // Rescheduling on launch also resets the 48-hour lapse reminder, which is exactly
        // right: it should measure silence since the player last opened the game.
        NotificationScheduler.RescheduleAll();
    }

    private void Update()
    {
        // Drain the backlog on a slow cadence. The queue is usually empty; this only does
        // real work in the window between launch and PlayFab authenticating.
        if (GameAnalytics.PendingCount == 0) return;

        if (Time.realtimeSinceStartup >= nextFlushTime)
        {
            nextFlushTime = Time.realtimeSinceStartup + FlushIntervalSeconds;
            GameAnalytics.Flush();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            ReportSessionEnd();
        }
        else
        {
            // Returning from background restarts the clock for the next session report.
            sessionStartTime = Time.realtimeSinceStartup;
            sessionEndReported = false;
            GameAnalytics.Flush();
        }
    }

    private void OnApplicationQuit()
    {
        ReportSessionEnd();
    }

    private void ReportSessionEnd()
    {
        if (sessionEndReported) return;
        sessionEndReported = true;

        float duration = Time.realtimeSinceStartup - sessionStartTime;
        GameAnalytics.SessionEnd(duration, FtueState.RunsThisSession);
        GameAnalytics.Flush();

        // PlayerPrefs are written by the individual setters, but a pause is the last
        // reliable moment to guarantee they reach disk on Android.
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
