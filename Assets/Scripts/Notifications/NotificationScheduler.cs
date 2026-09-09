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
/// Permission is asked for on the first launch, right after the player picks a language and
/// before the tutorial begins. On Android 13+ that ask is effectively one-shot — a denial
/// there is close to permanent — so it is paired with two recovery routes: a backstop ask
/// after the first temple for anyone the first one missed, and a Settings toggle that either
/// re-prompts or deep-links to the app's notification settings when the OS won't prompt again.
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

    /// <summary>
    /// True once an automatic ask has fired. Guards the automatic asks only — the Settings
    /// opt-in deliberately ignores it.
    /// </summary>
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
    /// Where a permission request came from, so the analytics event can tell the first-launch
    /// ask apart from a player deliberately opting back in from Settings months later.
    /// </summary>
    public enum PermissionSource
    {
        /// <summary>First launch, straight after the language picker.</summary>
        Onboarding,

        /// <summary>Backstop for anyone the onboarding ask never reached.</summary>
        FirstTemple,

        /// <summary>The player asked for it themselves, from the Settings toggle.</summary>
        Settings
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private const string PostNotificationsPermission = "android.permission.POST_NOTIFICATIONS";
#endif

    /// <summary>
    /// True when the OS will currently let us post. Off Android this reads true, so the
    /// settings UI doesn't render as blocked in the editor where there's nothing to grant.
    /// </summary>
    public static bool SystemPermissionGranted
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Notifications] Permission query failed: {e.Message}");
                return false;
            }
#else
            return true;
#endif
        }
    }

    /// <summary>
    /// True when Android will no longer show the system dialog — the player denied it for
    /// good, or switched notifications off in system settings afterwards. The only route
    /// left open is the app's own notification settings screen, which is where the toggle
    /// sends them instead of failing silently.
    /// </summary>
    public static bool PermissionPermanentlyDenied
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var status = AndroidNotificationCenter.UserPermissionToPost;
                if (status == PermissionStatus.Allowed) return false;
                if (status == PermissionStatus.NotificationsBlockedForApp) return true;

                // Denied once: Android still shows the dialog, and asks us to explain why
                // first. Denied for good: that rationale flag goes false and the dialog is
                // never shown again.
                return status == PermissionStatus.Denied
                       && !AndroidNotificationCenter.ShouldShowPermissionToPostRationale;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Notifications] Permission query failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// The first-launch ask, fired once the player has picked a language and before the
    /// tutorial starts — the placement most games use, and the only point in the first
    /// session where the game isn't mid-sentence.
    ///
    /// Worth knowing what this costs: on Android 13+ a denial here is close to permanent,
    /// and a player asked before they care usually denies. The Settings toggle is what buys
    /// that back, so the two are a pair rather than two independent features.
    /// </summary>
    public static void RequestPermissionOnboarding(Action<bool> onResult = null)
    {
        RequestSystemPermission(PermissionSource.Onboarding, onResult);
    }

    /// <summary>
    /// Backstop for anyone the onboarding ask never reached — a player who killed the app
    /// during the language picker, or who upgraded from a build that never asked at all.
    /// The HasRequestedPermission latch means this can't produce a second dialog.
    /// </summary>
    public static void RequestPermissionIfEarned()
    {
        if (!FtueState.HasCompletedFirstTemple) return;
        RequestSystemPermission(PermissionSource.FirstTemple, null);
    }

    /// <summary>
    /// The opt-in route from Settings, for a player who said no once and changed their mind.
    /// Deliberately not rationed by the automatic-ask latch: this is a button they pressed.
    ///
    /// Three outcomes, in the order Android allows them: already granted, the system dialog,
    /// or — when the dialog is gone for good — the app's notification settings screen.
    /// </summary>
    public static void RequestPermissionFromSettings(Action<bool> onResult = null)
    {
        if (SystemPermissionGranted)
        {
            onResult?.Invoke(true);
            return;
        }

        if (PermissionPermanentlyDenied)
        {
            OpenSystemNotificationSettings();
            // Whether they flipped the switch over there is only knowable once they come
            // back, so the caller hears "not yet" and re-reads the real state on resume.
            onResult?.Invoke(false);
            return;
        }

        RequestSystemPermission(PermissionSource.Settings, onResult);
    }

    private static void RequestSystemPermission(PermissionSource source, Action<bool> onResult)
    {
        bool automatic = source != PermissionSource.Settings;

        // The automatic asks share a single shot between them, ever. Settings is a
        // deliberate act and is never rationed.
        if (automatic && HasRequestedPermission)
        {
            onResult?.Invoke(SystemPermissionGranted);
            return;
        }

        if (automatic)
        {
            PlayerPrefs.SetInt(PP_PERMISSION_ASKED, 1);
            PlayerPrefs.Save();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (SystemPermissionGranted)
            {
                CompletePermissionRequest(source, true, onResult);
                return;
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => CompletePermissionRequest(source, true, onResult);
            callbacks.PermissionDenied += _ => CompletePermissionRequest(source, false, onResult);

            Permission.RequestUserPermission(PostNotificationsPermission, callbacks);
        }
        catch (Exception e)
        {
            // A failed ask must never take the launch sequence down with it.
            Debug.LogWarning($"[Notifications] Permission request failed: {e.Message}");
            onResult?.Invoke(false);
        }
#else
        Debug.Log($"[Notifications] Permission request skipped ({source}) — not an Android device build.");
        onResult?.Invoke(true);
#endif
    }

    private static void CompletePermissionRequest(PermissionSource source, bool granted, Action<bool> onResult)
    {
        GameAnalytics.NotificationPermission(granted, source.ToString());

        if (granted) RescheduleAll();

        onResult?.Invoke(granted);
    }

    /// <summary>
    /// Opens this app's notification settings screen. The last route left once Android has
    /// stopped showing the permission dialog, and the reason the Settings toggle isn't a
    /// dead end for a player who denied on first launch.
    /// </summary>
    public static void OpenSystemNotificationSettings()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                string packageName = activity.Call<string>("getPackageName");

                if (DeviceApiLevel >= 26)
                {
                    // Lands directly on the app's notification switch.
                    intent.Call<AndroidJavaObject>("setAction", "android.settings.APP_NOTIFICATION_SETTINGS");
                    intent.Call<AndroidJavaObject>("putExtra", "android.provider.extra.APP_PACKAGE", packageName);
                }
                else
                {
                    // minSdk is 25, where that screen doesn't exist yet. The app details page
                    // has the same switch one level down, which is as close as Android allows.
                    intent.Call<AndroidJavaObject>("setAction", "android.settings.APPLICATION_DETAILS_SETTINGS");
                    using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                    using (var uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", packageName, null))
                    {
                        intent.Call<AndroidJavaObject>("setData", uri);
                    }
                }

                intent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
                activity.Call("startActivity", intent);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Notifications] Could not open system notification settings: {e.Message}");
        }
#else
        Debug.Log("[Notifications] System notification settings deep link is Android-only.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static int DeviceApiLevel
    {
        get
        {
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    return version.GetStatic<int>("SDK_INT");
            }
            catch
            {
                return 0;
            }
        }
    }
#endif

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

    /// <summary>
    /// Forgets that we ever asked, along with the learned play hour and the player's toggle.
    /// Without this the first-launch ask can only be tested by uninstalling, because the
    /// HasRequestedPermission latch survives a data reset.
    ///
    /// Note the OS permission itself is NOT reset — Android owns that, and only an uninstall
    /// or the system settings screen can move it.
    /// </summary>
    public static void ResetAll()
    {
        CancelAll();

        PlayerPrefs.DeleteKey(PP_PERMISSION_ASKED);
        PlayerPrefs.DeleteKey(PP_ENABLED);
        PlayerPrefs.DeleteKey(PP_PLAY_HOUR);
        PlayerPrefs.Save();
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

    /// <summary>
    /// The next time the daily reminder should fire.
    ///
    /// Always tomorrow, never later today — every caller of RescheduleAll runs while the
    /// player is in the app, so today's reminder has already been made pointless by the fact
    /// that they're here. Reminding someone to play a game they are currently playing is the
    /// fastest way to get reminders turned off.
    ///
    /// The daily repeat then carries it forward on its own, so a player who stops opening the
    /// game keeps getting nudged, and a player who opens it daily keeps pushing the next one
    /// out by a day and effectively never sees it.
    /// </summary>
    private static DateTime NextOccurrenceOfHour(int hour)
    {
        DateTime now = DateTime.Now;
        DateTime today = new DateTime(now.Year, now.Month, now.Day, hour, 0, 0, DateTimeKind.Local);
        return today.AddDays(1);
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
