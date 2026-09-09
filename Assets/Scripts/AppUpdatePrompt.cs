using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANDROID && PLAY_APP_UPDATE
using Google.Play.AppUpdate;
#endif

/// <summary>
/// Google Play in-app updates, flexible flow.
///
/// Tells the player a newer build exists without sending them to the Play Store listing,
/// downloads it in the background while they keep playing, and then offers to restart into
/// it. Three moments, in order:
///
///   1. On reaching the main menu, ask Play whether an update is available. If it is — and
///      the player has not snoozed this exact version recently — show the ask.
///   2. On "Update", hand off to Play's own confirmation dialog via StartUpdate. Play owns
///      the consent step; our modal only decides *when* to raise it, which is the whole
///      point of the API. From there the download runs in the background and we show a
///      quiet progress strip.
///   3. When the download lands, offer to restart. CompleteUpdate() installs and relaunches
///      the app, so nothing after that call is reached on success.
///
/// The same check also catches an update that finished downloading in a previous session
/// but was never installed — Play reports it as already Downloaded, and the player gets the
/// restart prompt straight away rather than downloading it a second time.
///
/// Availability. This is Android-only and, more sharply, *Play-only*: an APK that Play did
/// not install — a sideloaded build, an editor run, a CI artifact — always reports "no
/// update available". It cannot be tested from a local build; use an internal test track or
/// Internal App Sharing, with a published versionCode higher than the one on the device.
///
/// The Google.Play.AppUpdate types only exist once the In-App Update plugin is imported, so
/// everything that touches them sits behind PLAY_APP_UPDATE. That symbol is added and
/// removed automatically by PlayAppUpdateDefine in the Editor folder, which means this file
/// compiles and quietly does nothing until the plugin is actually present.
/// </summary>
public class AppUpdatePrompt : MonoBehaviour
{
    #region Tuning

    /// <summary>
    /// How long "Not now" lasts, in days. Long enough that the prompt is not nagging, short
    /// enough that a player who declined once still gets the fix in the next release.
    /// </summary>
    private const int SnoozeDays = 3;

    /// <summary>
    /// Play priority (0-5, set per-release in the Play Console) at or above which the snooze
    /// is ignored and the ask is raised on every launch. Reserve it for releases the player
    /// genuinely needs — a crash fix, a save-breaking change.
    /// </summary>
    private const int SnoozeOverridePriority = 4;

    /// <summary>
    /// Days Play must have known about the update before we mention it. Play rolls releases
    /// out gradually, so on day zero an update may be visible to this device and to almost
    /// nobody else; waiting a day avoids prompting into a rollout that gets halted. Set to 0
    /// to ask as soon as Play offers.
    /// </summary>
    private const int MinStalenessDays = 1;

    /// <summary>Seconds to wait after the menu loads before asking Play.</summary>
    private const float StartupDelaySeconds = 2f;

    #endregion

    private const string SnoozedVersionKey = "AppUpdate_SnoozedVersion";
    private const string SnoozedAtKey = "AppUpdate_SnoozedAt";

    private static AppUpdatePrompt instance;

    private AppUpdatePromptView promptView;
    private bool hasCheckedThisSession;

    #region Bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureInstance();

    private static void EnsureInstance()
    {
        if (instance != null) return;

        // Main menu only. Interrupting a run to talk about app versions is the one thing
        // this feature must never do.
        if (!SceneLoader.IsInMainMenu()) return;

        var go = new GameObject("AppUpdatePrompt");
        instance = go.AddComponent<AppUpdatePrompt>();
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
        DependencyRegistry.Register<AppUpdatePrompt>(this);
    }

    private void Start()
    {
        StartCoroutine(CheckAfterDelay());
    }

    private void OnDestroy()
    {
        DependencyRegistry.Unregister<AppUpdatePrompt>(this);
        if (instance == this) instance = null;
    }

    #endregion

    private IEnumerator CheckAfterDelay()
    {
        if (hasCheckedThisSession) yield break;
        hasCheckedThisSession = true;

        // Let the menu finish appearing first — a modal that lands on top of a scene still
        // fading in reads as a bug.
        yield return new WaitForSeconds(StartupDelaySeconds);

        yield return CheckForUpdate();
    }

#if UNITY_ANDROID && PLAY_APP_UPDATE

    private AppUpdateManager updateManager;
    private AppUpdateInfo pendingInfo;

    private IEnumerator CheckForUpdate()
    {
        if (Application.platform != RuntimePlatform.Android) yield break;

        // Constructing the manager reaches into the Play Core AAR through JNI, which throws
        // rather than returning null when the native side is missing. Bail out quietly
        // instead of letting that escape into the coroutine.
        if (updateManager == null)
        {
            try
            {
                updateManager = new AppUpdateManager();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AppUpdate] Could not create AppUpdateManager: {e.Message}");
            }

            if (updateManager == null) yield break;
        }

        var infoOperation = updateManager.GetAppUpdateInfo();
        yield return infoOperation;

        if (!infoOperation.IsSuccessful)
        {
            // Expected and harmless on any build Play did not install.
            Debug.Log($"[AppUpdate] No update info: {infoOperation.Error}");
            yield break;
        }

        AppUpdateInfo info = infoOperation.GetResult();
        pendingInfo = info;

        // An update downloaded in an earlier session and never installed. Skip straight to
        // the restart offer; downloading it again would waste the player's data.
        if (info.AppUpdateStatus == AppUpdateStatus.Downloaded)
        {
            GameAnalytics.AppUpdate("already_downloaded", info.AvailableVersionCode);
            ShowRestartPrompt();
            yield break;
        }

        if (info.UpdateAvailability != UpdateAvailability.UpdateAvailable)
        {
            Debug.Log($"[AppUpdate] Availability: {info.UpdateAvailability}");
            yield break;
        }

        var options = AppUpdateOptions.FlexibleAppUpdateOptions();
        if (!info.IsUpdateTypeAllowed(options))
        {
            // Play can refuse a flexible update — metered connection rules, device state.
            GameAnalytics.AppUpdate("not_allowed", info.AvailableVersionCode);
            yield break;
        }

        int staleness = info.ClientVersionStalenessDays ?? 0;
        if (staleness < MinStalenessDays && info.UpdatePriority < SnoozeOverridePriority)
        {
            Debug.Log($"[AppUpdate] Update {info.AvailableVersionCode} is only {staleness} day(s) old; waiting.");
            yield break;
        }

        if (IsSnoozed(info))
        {
            Debug.Log($"[AppUpdate] Update {info.AvailableVersionCode} is snoozed.");
            yield break;
        }

        GameAnalytics.AppUpdate("prompt_shown", info.AvailableVersionCode);
        ShowAvailablePrompt();
    }

    private void ShowAvailablePrompt()
    {
        EnsureView().ShowModal(
            LocalizationManager.Get("update_available_title"),
            LocalizationManager.Get("update_available_body"),
            LocalizationManager.Get("update_button_update"), OnUpdateAccepted,
            LocalizationManager.Get("update_button_later"), OnUpdateDeclined);
    }

    private void OnUpdateDeclined()
    {
        GameAnalytics.AppUpdate("declined", pendingInfo.AvailableVersionCode);
        Snooze(pendingInfo);
        EnsureView().Hide();
    }

    private void OnUpdateAccepted()
    {
        GameAnalytics.AppUpdate("accepted", pendingInfo.AvailableVersionCode);
        // Clear any snooze: this version is now wanted, not deferred.
        PlayerPrefs.DeleteKey(SnoozedVersionKey);
        PlayerPrefs.DeleteKey(SnoozedAtKey);
        StartCoroutine(RunFlexibleUpdate());
    }

    private IEnumerator RunFlexibleUpdate()
    {
        var view = EnsureView();

        // Show the strip immediately: Play's own confirmation dialog covers the screen, and
        // when the player dismisses it they should land on something that explains itself.
        view.ShowProgress(LocalizationManager.Get("update_downloading", 0), 0f);

        AppUpdateRequest request = null;
        try
        {
            request = updateManager.StartUpdate(pendingInfo, AppUpdateOptions.FlexibleAppUpdateOptions());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AppUpdate] StartUpdate threw: {e.Message}");
            GameAnalytics.AppUpdate("start_failed", pendingInfo.AvailableVersionCode, e.GetType().Name);
        }

        if (request == null)
        {
            view.Hide();
            yield break;
        }

        while (!request.IsDone)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(request.DownloadProgress) * 100f);
            view.ShowProgress(LocalizationManager.Get("update_downloading", percent), request.DownloadProgress);
            yield return null;
        }

        if (request.Error != AppUpdateErrorCode.NoError)
        {
            // ErrorUserCanceled is the ordinary "player said no to Play's dialog" case, so
            // this is logged rather than surfaced — a failure toast for a deliberate cancel
            // would be worse than silence.
            Debug.Log($"[AppUpdate] Update did not complete: {request.Error}");
            GameAnalytics.AppUpdate("download_failed", pendingInfo.AvailableVersionCode, request.Error.ToString());

            // Treat a cancel like a "not now" so the player is not re-asked on next launch.
            if (request.Error == AppUpdateErrorCode.ErrorUserCanceled) Snooze(pendingInfo);

            view.Hide();
            yield break;
        }

        GameAnalytics.AppUpdate("downloaded", pendingInfo.AvailableVersionCode);
        ShowRestartPrompt();
    }

    private void ShowRestartPrompt()
    {
        GameAnalytics.AppUpdate("restart_prompt", pendingInfo.AvailableVersionCode);
        EnsureView().ShowModal(
            LocalizationManager.Get("update_ready_title"),
            LocalizationManager.Get("update_ready_body"),
            LocalizationManager.Get("update_button_restart"), OnRestartAccepted,
            LocalizationManager.Get("update_button_later"), OnRestartDeclined);
    }

    private void OnRestartDeclined()
    {
        // No snooze here. The bytes are already on the device, so re-offering next launch
        // costs the player nothing and the install is one tap away.
        GameAnalytics.AppUpdate("restart_declined", pendingInfo.AvailableVersionCode);
        EnsureView().Hide();
    }

    private void OnRestartAccepted()
    {
        GameAnalytics.AppUpdate("restart_accepted", pendingInfo.AvailableVersionCode);
        StartCoroutine(CompleteUpdate());
    }

    private IEnumerator CompleteUpdate()
    {
        var operation = updateManager.CompleteUpdate();
        yield return operation;

        // Reached only on failure: a successful install restarts the app.
        Debug.LogWarning($"[AppUpdate] CompleteUpdate failed: {operation.Error}");
        GameAnalytics.AppUpdate("install_failed", pendingInfo.AvailableVersionCode, operation.Error.ToString());
        EnsureView().Hide();
    }

    #region Snooze

    /// <summary>
    /// True while a recent "Not now" still stands. The snooze is per-version-code, so the
    /// next release asks again immediately instead of inheriting the deferral; a
    /// high-priority release ignores it outright.
    /// </summary>
    private static bool IsSnoozed(AppUpdateInfo info)
    {
        if (info.UpdatePriority >= SnoozeOverridePriority) return false;
        if (PlayerPrefs.GetInt(SnoozedVersionKey, 0) != info.AvailableVersionCode) return false;

        string stored = PlayerPrefs.GetString(SnoozedAtKey, string.Empty);
        if (!long.TryParse(stored, out long ticks)) return false;

        var snoozedAt = new System.DateTime(ticks, System.DateTimeKind.Utc);
        return System.DateTime.UtcNow - snoozedAt < System.TimeSpan.FromDays(SnoozeDays);
    }

    private static void Snooze(AppUpdateInfo info)
    {
        PlayerPrefs.SetInt(SnoozedVersionKey, info.AvailableVersionCode);
        PlayerPrefs.SetString(SnoozedAtKey, System.DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();
    }

    #endregion

#else

    /// <summary>
    /// Stub for every platform and configuration without the Play In-App Update plugin.
    /// Import it (and let PlayAppUpdateDefine add PLAY_APP_UPDATE) to switch the real
    /// implementation on.
    /// </summary>
    private IEnumerator CheckForUpdate()
    {
        yield break;
    }

#endif

    private AppUpdatePromptView EnsureView()
    {
        if (promptView == null) promptView = AppUpdatePromptView.Create();
        return promptView;
    }
}
