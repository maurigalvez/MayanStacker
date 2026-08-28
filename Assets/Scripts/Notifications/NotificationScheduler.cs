using System;
using UnityEngine;

#if UNITY_ANDROID
using Unity.Notifications.Android;
using UnityEngine.Android;
#endif

/// <summary>
/// Local notifications — the return hook the Daily Challenge has been missing.
///
/// Two reminders, deliberately no more:
///   1. A daily nudge at the hour the player usually plays, naming today's ritual and the
///      streak they'd be defending.
///   2. A single lapse reminder after 48 hours of silence, naming the site they stopped on.
///
/// Permission is requested after the first temple is completed, never on launch — asking a
/// stranger for notification access before they've played is how you get a permanent "no".
///
/// ---------------------------------------------------------------------------------
/// DEPENDENCY: com.unity.mobile.notifications (added to Packages/manifest.json).
///
/// This is the only file in the project that needs that package. Unity imports it the
/// first time the project is opened with a network connection. If the import fails (no
/// connection, registry down) this file will not compile until it succeeds — nothing
/// else in the FTUE/retention work depends on it.
///
/// To carry on without it: delete this file and the four calls to NotificationScheduler
/// (AnalyticsSession, UIManager x2, PlayerDataReset). Everything else keeps working.
/// ---------------------------------------------------------------------------------
///
/// Every method is guarded and exception-safe: off Android, and in the editor, they are
/// no-ops rather than errors.
/// </summary>
public static class NotificationScheduler
{
    private const string ChannelId = "tamalstacker_ritual";
    private const string PP_PLAY_HOUR = "Notif_UsualPlayHour";
    private const string PP_PERMISSION_ASKED = "Notif_PermissionAsked";
    private const string PP_ENABLED = "Notif_Enabled";

    private const int DailyReminderId = 1001;
    private const int LapseReminderId = 1002;

    private const int LapseReminderHours = 48;
    private const int DefaultPlayHour = 19; // 7pm local, if we haven't learned better yet

    /// <summary>Player-facing toggle. Defaults on; surfaced in settings.</summary>
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PP_ENABLED, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(PP_ENABLED, value ? 1 : 0);
            PlayerPrefs.Save();
            RescheduleAll();
        }
    }

    /// <summary>True once we've asked for permission, so we never ask twice.</summary>
    public static bool HasRequestedPermission => PlayerPrefs.GetInt(PP_PERMISSION_ASKED, 0) == 1;

    #region Learning when they play

    /// <summary>
    /// Remembers the local hour the player actually plays, so the daily reminder lands when
    /// they're likely free rather than at an arbitrary fixed time.
    /// </summary>
    public static void RecordPlaySession()
    {
        PlayerPrefs.SetInt(PP_PLAY_HOUR, DateTime.Now.Hour);
        PlayerPrefs.Save();
    }

    private static int UsualPlayHour
    {
        get
        {
            int hour = PlayerPrefs.GetInt(PP_PLAY_HOUR, DefaultPlayHour);
            return Mathf.Clamp(hour, 0, 23);
        }
    }

    #endregion

    #region Permission

    /// <summary>
    /// Asks for notification permission — but only once, and only after the player has
    /// finished a temple, so the ask arrives with some earned goodwill behind it.
    /// </summary>
    public static void RequestPermissionIfEarned()
    {
        if (HasRequestedPermission) return;
        if (!FtueState.HasCompletedFirstTemple) return;

        PlayerPrefs.SetInt(PP_PERMISSION_ASKED, 1);
        PlayerPrefs.Save();

#if UNITY_ANDROID && !UNITY_EDITOR
        // POST_NOTIFICATIONS is required from Android 13 (API 33) onward.
        const string permission = "android.permission.POST_NOTIFICATIONS";
        if (!Permission.HasUserAuthorizedPermission(permission))
        {
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ =>
            {
                GameAnalytics.NotificationPermission(true);
                RescheduleAll();
            };
            callbacks.PermissionDenied += _ => GameAnalytics.NotificationPermission(false);

            Permission.RequestUserPermission(permission, callbacks);
            return;
        }

        GameAnalytics.NotificationPermission(true);
        RescheduleAll();
#else
        Debug.Log("[Notifications] Permission request skipped (not an Android device build).");
#endif
    }

    #endregion

    #region Scheduling

    /// <summary>
    /// Clears and rebuilds both reminders from current state. Cheap and idempotent — call
    /// it whenever something that shapes the copy changes (a run finishes, the streak moves,
    /// the player toggles the setting).
    /// </summary>
    public static void RescheduleAll()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            EnsureChannel();

            AndroidNotificationCenter.CancelScheduledNotification(DailyReminderId);
            AndroidNotificationCenter.CancelScheduledNotification(LapseReminderId);

            if (!Enabled) return;

            ScheduleDailyReminder();
            ScheduleLapseReminder();
        }
        catch (Exception e)
        {
            // A failed reminder must never take the game down with it.
            Debug.LogWarning($"[Notifications] Reschedule failed: {e.Message}");
        }
#endif
    }

    /// <summary>Cancels everything — used when the player turns reminders off or resets data.</summary>
    public static void CancelAll()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidNotificationCenter.CancelAllScheduledNotifications();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Notifications] Cancel failed: {e.Message}");
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void EnsureChannel()
    {
        var channel = new AndroidNotificationChannel
        {
            Id = ChannelId,
            Name = LocalizationManager.Get("notif_channel_name"),
            Description = LocalizationManager.Get("notif_channel_description"),
            Importance = Importance.Default
        };

        AndroidNotificationCenter.RegisterNotificationChannel(channel);
    }

    private static void ScheduleDailyReminder()
    {
        DateTime fireTime = NextOccurrenceOfHour(UsualPlayHour);

        var notification = new AndroidNotification
        {
            Title = LocalizationManager.Get("notif_daily_title"),
            Text = BuildDailyBody(),
            FireTime = fireTime,
            RepeatInterval = TimeSpan.FromDays(1),
            SmallIcon = "icon_0",
            LargeIcon = "icon_1"
        };

        AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, DailyReminderId);
    }

    private static void ScheduleLapseReminder()
    {
        var notification = new AndroidNotification
        {
            Title = LocalizationManager.Get("notif_lapse_title"),
            Text = BuildLapseBody(),
            FireTime = DateTime.Now.AddHours(LapseReminderHours),
            SmallIcon = "icon_0",
            LargeIcon = "icon_1"
        };

        AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, LapseReminderId);
    }

    private static DateTime NextOccurrenceOfHour(int hour)
    {
        DateTime now = DateTime.Now;
        DateTime candidate = new DateTime(now.Year, now.Month, now.Day, hour, 0, 0, DateTimeKind.Local);
        return candidate <= now ? candidate.AddDays(1) : candidate;
    }
#endif

    /// <summary>
    /// The daily body carries the streak when there is one to defend — that number is what
    /// makes the notification worth tapping.
    /// </summary>
    private static string BuildDailyBody()
    {
        int streak = DailyStreak.Current;

        return streak > 1
            ? LocalizationManager.Get("notif_daily_body_streak", streak)
            : LocalizationManager.Get("notif_daily_body");
    }

    /// <summary>
    /// The lapse body names the exact site they stopped on. "Come back" is ignorable;
    /// "Yaxha is still unfinished" is not.
    /// </summary>
    private static string BuildLapseBody()
    {
        var levelManager = DependencyRegistry.Find<LevelManager>();
        if (levelManager != null)
        {
            for (int i = 0; i < levelManager.TotalLevels; i++)
            {
                var levels = levelManager.GetAllLevels();
                if (i >= levels.Count) break;

                var level = levels[i];
                if (level == null) continue;

                if (levelManager.GetLevelStars(level.levelNumber) <= 0)
                {
                    string siteName = LocalizationManager.GetLevelName(level);
                    return LocalizationManager.Get("notif_lapse_body_site", siteName);
                }
            }
        }

        return LocalizationManager.Get("notif_lapse_body");
    }

    #endregion
}
