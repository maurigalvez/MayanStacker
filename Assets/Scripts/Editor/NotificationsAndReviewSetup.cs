#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor tool that builds and wires the two player-facing controls this feature needs into
/// the Settings panel of the currently open scene:
///
///   • a "Daily Reminders" toggle — the opt-in route for anyone who denied the first-launch
///     permission dialog, which on Android 13+ is otherwise a one-way door;
///   • a "Rate this game" button — the only way a player can offer feedback on their own
///     initiative, since Google's automatic in-app review sheet is quota-limited and often
///     never appears.
///
/// It follows the same contract as the other setup tools in this folder: idempotent and
/// non-destructive. A SettingsManager field that is already assigned is left alone, objects
/// are found by name before being created, and re-running produces no duplicates. The
/// placeholder styling uses the shared Mayan-temple palette from
/// <see cref="DailyChallengeUISetup"/> — restyle it in the scene afterward.
///
/// Menu: TamalStacker ▸ Retention ▸ Set Up Notification &amp; Review Settings
/// </summary>
public static class NotificationsAndReviewSetup
{
    [MenuItem("TamalStacker/Retention/Set Up Notification & Review Settings")]
    public static void SetupSettingsPanel()
    {
        var settings = Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        if (settings == null)
        {
            EditorUtility.DisplayDialog("Notification & Review Settings",
                "No SettingsManager found in the open scene.\n\nOpen the MainMenu scene first, then run this again.",
                "OK");
            return;
        }

        Transform panel = ResolveSettingsPanel(settings);
        if (panel == null)
        {
            EditorUtility.DisplayDialog("Notification & Review Settings",
                "Couldn't find a Settings panel to build into.\n\n" +
                "Assign MainMenuManager's 'settingsPanel' field, or put the SettingsManager under the panel it drives, then run this again.",
                "OK");
            return;
        }

        var so = new SerializedObject(settings);
        int created = 0, skipped = 0;

        EnsureNotificationsToggle(so, panel, ref created, ref skipped);
        EnsureStatusText(so, panel, ref created, ref skipped);
        EnsureRateButton(so, panel, ref created, ref skipped);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
        EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;

        DailyChallengeUISetup.Report("Notification & Review Settings", created, skipped);
    }

    /// <summary>
    /// The panel to build into. SettingsManager has no reference to its own panel, so the
    /// MainMenuManager's wiring is the authority; failing that, the object the manager
    /// itself lives on is the next best guess.
    /// </summary>
    private static Transform ResolveSettingsPanel(SettingsManager settings)
    {
        var menu = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (menu != null)
        {
            var menuSo = new SerializedObject(menu);
            var prop = menuSo.FindProperty("settingsPanel");
            if (prop != null && prop.objectReferenceValue is GameObject panel) return panel.transform;
        }

        // Fallback: the manager is often a child of the panel it drives.
        var rect = settings.GetComponent<RectTransform>();
        if (rect != null) return rect;

        return settings.transform.parent;
    }

    private static void EnsureNotificationsToggle(SerializedObject so, Transform panel, ref int created, ref int skipped)
    {
        var prop = so.FindProperty("notificationsToggle");
        if (prop == null)
        {
            Debug.LogWarning("[NotificationsAndReviewSetup] SettingsManager has no 'notificationsToggle' field.");
            return;
        }
        if (prop.objectReferenceValue != null) { skipped++; return; }

        GameObject row = DailyChallengeUISetup.FindOrCreate(panel, "NotificationsToggle", out _);
        var rowRect = row.GetComponent<RectTransform>();
        DailyChallengeUISetup.PlaceBottom(300f, 640f, 80f)(rowRect);

        // A uGUI Toggle needs its own background graphic and a separate checkmark it can
        // switch on and off; without both assigned the control renders but never changes.
        GameObject box = DailyChallengeUISetup.FindOrCreate(row.transform, "Background", out _);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0.5f);
        boxRect.pivot = new Vector2(0f, 0.5f);
        boxRect.sizeDelta = new Vector2(64f, 64f);
        boxRect.anchoredPosition = new Vector2(0f, 0f);
        boxRect.localScale = Vector3.one;
        var boxImage = box.GetComponent<Image>() ?? Undo.AddComponent<Image>(box);
        boxImage.color = new Color(0.13f, 0.16f, 0.15f, 1f); // stone

        GameObject check = DailyChallengeUISetup.FindOrCreate(box.transform, "Checkmark", out _);
        var checkRect = check.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.18f, 0.18f);
        checkRect.anchorMax = new Vector2(0.82f, 0.82f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        checkRect.localScale = Vector3.one;
        var checkImage = check.GetComponent<Image>() ?? Undo.AddComponent<Image>(check);
        checkImage.color = DailyChallengeUISetup.Jade;

        var label = DailyChallengeUISetup.CreateText(row.transform, "Label", "Daily Reminders", 34,
            DailyChallengeUISetup.Parchment, rt =>
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(84f, 0f);
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            });
        label.alignment = TextAlignmentOptions.MidlineLeft;
        EnsureLocalizedText(label.gameObject, "settings_notifications");

        var toggle = row.GetComponent<Toggle>() ?? Undo.AddComponent<Toggle>(row);
        toggle.targetGraphic = boxImage;
        toggle.graphic = checkImage;
        // The real value is painted by SettingsManager the moment the panel opens, so this
        // is only what the control looks like sitting in the scene.
        toggle.isOn = true;

        prop.objectReferenceValue = toggle;
        created++;
    }

    private static void EnsureStatusText(SerializedObject so, Transform panel, ref int created, ref int skipped)
    {
        var prop = so.FindProperty("notificationsStatusText");
        if (prop == null)
        {
            Debug.LogWarning("[NotificationsAndReviewSetup] SettingsManager has no 'notificationsStatusText' field.");
            return;
        }
        if (prop.objectReferenceValue != null) { skipped++; return; }

        var text = DailyChallengeUISetup.CreateText(panel, "NotificationsStatus",
            "Reminders are switched off in your device settings.", 22,
            new Color(0.72f, 0.68f, 0.58f, 1f),
            DailyChallengeUISetup.PlaceBottom(250f, 640f, 44f));
        text.alignment = TextAlignmentOptions.MidlineLeft;
        EnsureLocalizedText(text.gameObject, "settings_notifications_blocked");

        // Only shown when Android is actually blocking us; SettingsManager turns it back on.
        text.gameObject.SetActive(false);

        prop.objectReferenceValue = text;
        created++;
    }

    private static void EnsureRateButton(SerializedObject so, Transform panel, ref int created, ref int skipped)
    {
        var prop = so.FindProperty("rateGameButton");
        if (prop == null)
        {
            Debug.LogWarning("[NotificationsAndReviewSetup] SettingsManager has no 'rateGameButton' field.");
            return;
        }
        if (prop.objectReferenceValue != null) { skipped++; return; }

        GameObject go = DailyChallengeUISetup.FindOrCreate(panel, "RateGameButton", out _);
        var rect = go.GetComponent<RectTransform>();
        DailyChallengeUISetup.PlaceBottom(150f, 480f, 90f)(rect);

        var image = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
        image.color = DailyChallengeUISetup.Jade;

        var button = go.GetComponent<Button>() ?? Undo.AddComponent<Button>(go);
        button.targetGraphic = image;

        var label = DailyChallengeUISetup.CreateText(go.transform, "Text", "Rate this game", 34,
            DailyChallengeUISetup.Parchment, null);
        var labelRect = label.GetComponent<RectTransform>();
        DailyChallengeUISetup.Stretch(labelRect);
        label.alignment = TextAlignmentOptions.Center;
        EnsureLocalizedText(label.gameObject, "settings_rate_game");

        prop.objectReferenceValue = button;
        created++;
    }

    /// <summary>
    /// Attaches a LocalizedText with the given key, so these labels follow the language
    /// picker like the rest of the UI instead of being frozen in English.
    /// </summary>
    private static void EnsureLocalizedText(GameObject go, string key)
    {
        var localized = go.GetComponent<LocalizedText>() ?? Undo.AddComponent<LocalizedText>(go);
        var so = new SerializedObject(localized);
        var prop = so.FindProperty("localizationKey");
        if (prop != null)
        {
            prop.stringValue = key;
            so.ApplyModifiedProperties();
        }
    }
}
#endif
