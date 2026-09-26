#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and wires the Remove Ads controls into the Settings panel of the open scene:
///
///   • "Remove Ads · $2.99" — the primary (jade) button; SettingsManager turns it into a
///     disabled "Ads removed — thank you!" once the purchase is owned;
///   • "Restore Purchases" — the secondary (clay) button, hidden once owned;
///   • a one-line status label for pending payments, failures and restore results.
///
/// Dressed from the house style (<see cref="UIHouseStyle"/>) so nothing needs styling by hand.
/// Same contract as the other setup tools: idempotent and non-destructive — a SettingsManager
/// field that is already assigned is left alone and re-running creates no duplicates. The row
/// sits at the bottom of the panel; move it if it overlaps anything there.
///
/// Menu: TamalStacker ▸ Monetization ▸ Set Up Remove Ads Settings (MainMenu scene open)
/// </summary>
public static class RemoveAdsSettingsSetup
{
    private const string Title = "Remove Ads Settings";

    private const float ButtonWidth = 360f;
    private const float ButtonHeight = 90f;
    private const float ButtonGap = 20f;
    private const float RowFromBottom = 50f;

    [MenuItem("TamalStacker/Monetization/Set Up Remove Ads Settings")]
    public static void Setup()
    {
        var settings = Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        if (settings == null)
        {
            EditorUtility.DisplayDialog(Title,
                "No SettingsManager found in the open scene.\n\nOpen the MainMenu scene first, then run this again.",
                "OK");
            return;
        }

        Transform panel = ResolveSettingsPanel(settings);
        if (panel == null)
        {
            EditorUtility.DisplayDialog(Title,
                "Couldn't find a Settings panel to build into.\n\n" +
                "Assign MainMenuManager's 'settingsPanel' field, then run this again.",
                "OK");
            return;
        }

        if (!UIHouseStyle.HasReference)
        {
            Debug.LogWarning($"[RemoveAdsSettingsSetup] {UIHouseStyle.ReferencePrefabPath} is missing — using the fallback house sprites.");
        }

        var so = new SerializedObject(settings);
        int created = 0, skipped = 0;

        // Secondary on the left, primary on the right — the house order for button pairs.
        float offset = (ButtonWidth + ButtonGap) * 0.5f;
        EnsureButton(so, "restorePurchasesButton", null, panel, "RestorePurchasesButton",
            "Restore Purchases", "settings_restore_purchases", UIHouseStyle.Secondary, -offset, ref created, ref skipped);
        EnsureButton(so, "removeAdsButton", "removeAdsLabel", panel, "RemoveAdsButton",
            "Remove Ads", null, UIHouseStyle.Primary, offset, ref created, ref skipped);
        EnsureStatusText(so, panel, ref created, ref skipped);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
        EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;

        DailyChallengeUISetup.Report(Title, created, skipped);
    }

    private static Transform ResolveSettingsPanel(SettingsManager settings)
    {
        var menu = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (menu != null)
        {
            var prop = new SerializedObject(menu).FindProperty("settingsPanel");
            if (prop != null && prop.objectReferenceValue is GameObject panel) return panel.transform;
        }

        var rect = settings.GetComponent<RectTransform>();
        return rect != null ? rect : settings.transform.parent;
    }

    /// <param name="localizationKey">
    /// Null for the Remove Ads label: SettingsManager writes it (price, thank-you), and a
    /// LocalizedText on it would overwrite that on every language change.
    /// </param>
    private static void EnsureButton(SerializedObject so, string field, string labelField, Transform panel,
        string name, string sample, string localizationKey, string role, float x,
        ref int created, ref int skipped)
    {
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[RemoveAdsSettingsSetup] SettingsManager has no '{field}' field.");
            return;
        }
        if (prop.objectReferenceValue != null) { skipped++; return; }

        GameObject go = DailyChallengeUISetup.FindOrCreate(panel, name, out _);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        rect.anchoredPosition = new Vector2(x, RowFromBottom);
        rect.localScale = Vector3.one;

        var image = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
        var button = go.GetComponent<Button>() ?? Undo.AddComponent<Button>(go);

        // "Label" is the child name UIHouseStyle copies the reference label's look onto.
        var label = DailyChallengeUISetup.CreateText(go.transform, "Label", sample, 34,
            DailyChallengeUISetup.Parchment, DailyChallengeUISetup.Stretch);
        label.alignment = TextAlignmentOptions.Center;

        UIHouseStyle.ApplyButton(button, role);
        button.targetGraphic = image;

        if (!string.IsNullOrEmpty(localizationKey)) EnsureLocalizedText(label.gameObject, localizationKey);

        prop.objectReferenceValue = button;
        if (!string.IsNullOrEmpty(labelField))
        {
            var labelProp = so.FindProperty(labelField);
            if (labelProp != null && labelProp.objectReferenceValue == null) labelProp.objectReferenceValue = label;
        }
        created++;
    }

    private static void EnsureStatusText(SerializedObject so, Transform panel, ref int created, ref int skipped)
    {
        var prop = so.FindProperty("purchaseStatusText");
        if (prop == null)
        {
            Debug.LogWarning("[RemoveAdsSettingsSetup] SettingsManager has no 'purchaseStatusText' field.");
            return;
        }
        if (prop.objectReferenceValue != null) { skipped++; return; }

        var text = DailyChallengeUISetup.CreateText(panel, "PurchaseStatus",
            "Purchases restored.", 24, DailyChallengeUISetup.Parchment,
            DailyChallengeUISetup.PlaceBottom(RowFromBottom - 44f, ButtonWidth * 2f + ButtonGap, 40f));
        UIHouseStyle.ApplyLabel(text, UIHouseStyle.Body);
        text.fontSize = 24;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center;

        // SettingsManager shows it only when there is something to say.
        text.gameObject.SetActive(false);

        prop.objectReferenceValue = text;
        created++;
    }

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
