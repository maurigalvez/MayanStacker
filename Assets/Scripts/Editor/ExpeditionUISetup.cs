#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the Weekly Expedition panel:
///   • Resources/UI/Expedition.prefab - the five stages with progress, badge count and week timer
///     (<see cref="ExpeditionView"/>), styled through <see cref="UIHouseStyle"/>. Kept if it already
///     exists, so hand styling survives a re-run.
///   • an "EXPEDITION" chip (<see cref="ExpeditionMenuChip"/>) on the Main Menu panel inside the Main
///     Menu Canvas prefab, under the Path chip. Skipped if one already exists.
///
/// Menus: TamalStacker ▸ UI ▸ Create Expedition Prefab, TamalStacker ▸ Economy ▸ Add Expedition Menu Chip.
/// </summary>
public static class ExpeditionUISetup
{
    private const string PrefabPath = "Assets/Resources/UI/Expedition.prefab";
    private const string MainMenuPrefabPath = "Assets/Prefabs/Main Menu/Main Menu Canvas.prefab";
    private const string ChipName = "ExpeditionChip";

    // Directly under the Path chip (-780, 120 tall) with a 20 px gap.
    private static readonly Vector2 ChipPosition = new Vector2(-30f, -920f);
    private static readonly Vector2 ChipSize = new Vector2(180f, 120f);

    [MenuItem("TamalStacker/UI/Create Expedition Prefab")]
    public static void CreatePrefab()
    {
        EditorUtility.DisplayDialog("Weekly Expedition", EnsurePrefab(), "OK");
    }

    [MenuItem("TamalStacker/Economy/Add Expedition Menu Chip")]
    public static void AddMenuChip()
    {
        EditorUtility.DisplayDialog("Weekly Expedition", EnsureChip(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI: prefab and menu chip.</summary>
    public static void CreateAllBatch()
    {
        EnsurePrefab();
        EnsureChip();
    }

    private static string EnsurePrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");

        string note;
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null && RowResolves())
        {
            note = $"Kept {PrefabPath}.";
        }
        else
        {
            GameObject root = BuildPanel();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            note = existing != null ? $"Rebuilt {PrefabPath} (its row script was missing)." : $"Created {PrefabPath}.";
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ExpeditionUISetup] {note}");
        return note;
    }

    private static GameObject BuildPanel()
    {
        var root = new GameObject("Expedition",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ExpeditionView));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ExpeditionView.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        var panel = RunOverlayUI.CreateChild("Panel", root.transform);
        RunOverlayUI.Stretch(panel);
        var dim = panel.gameObject.AddComponent<Image>();
        dim.color = RunOverlayUI.Backdrop;
        dim.raycastTarget = true;

        var modal = RunOverlayUI.CreateChild("Modal", panel);
        RunOverlayUI.Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1300f));
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;
        UIHouseStyle.ApplyImage(slab, UIHouseStyle.Modal);

        var title = Label("Title", modal, "Weekly Expedition", 60f, RunOverlayUI.Gold, UIHouseStyle.Title);
        RunOverlayUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(800f, 90f));

        var summary = Label("Summary", modal, "Weeks completed: 0", 36f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        summary.enableAutoSizing = true;
        summary.fontSizeMin = 22f;
        summary.fontSizeMax = 36f;
        RunOverlayUI.Place(summary.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(800f, 60f));

        var reset = Label("Reset", modal, "Resets in 6d 12h", 32f, RunOverlayUI.Gold, UIHouseStyle.Body);
        RunOverlayUI.Place(reset.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -235f), new Vector2(800f, 50f));

        // ── Stage list (five rows, no scrolling) ──
        var content = RunOverlayUI.CreateChild("Content", modal);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(50f, 230f);
        content.offsetMax = new Vector2(-50f, -290f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var row = BuildRow(modal);

        // ── Close ──
        var close = RunOverlayUI.CreateChild("CloseButton", modal);
        RunOverlayUI.Place(close, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(520f, 110f));
        var closeImage = close.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(closeImage, UIHouseStyle.Secondary);
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        UIHouseStyle.ApplyButton(closeButton, UIHouseStyle.Secondary);
        var closeLabel = Label("Label", close, "Close", 40f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        RunOverlayUI.Stretch(closeLabel.rectTransform);

        var so = new SerializedObject(root.GetComponent<ExpeditionView>());
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("summaryLabel").objectReferenceValue = summary;
        so.FindProperty("resetLabel").objectReferenceValue = reset;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("closeLabel").objectReferenceValue = closeLabel;
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("rowTemplate").objectReferenceValue = row;
        so.ApplyModifiedPropertiesWithoutUndo();
        SpinsPanelStyle.Dress(root);
        return root;
    }

    private static ExpeditionRow BuildRow(Transform parent)
    {
        var rect = RunOverlayUI.CreateChild("RowTemplate", parent);
        rect.sizeDelta = new Vector2(840f, 150f);
        var bg = rect.gameObject.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.07f);
        bg.raycastTarget = false;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 150f;
        rect.gameObject.AddComponent<CanvasGroup>();
        var row = rect.gameObject.AddComponent<ExpeditionRow>();

        // Gold edge on the left marks a cleared stage.
        var marker = RunOverlayUI.CreateChild("DoneMarker", rect);
        marker.anchorMin = new Vector2(0f, 0f);
        marker.anchorMax = new Vector2(0f, 1f);
        marker.pivot = new Vector2(0f, 0.5f);
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = new Vector2(10f, 0f);
        var markerImage = marker.gameObject.AddComponent<Image>();
        markerImage.color = RunOverlayUI.Gold;
        markerImage.raycastTarget = false;

        var stageLabel = Label("Stage", rect, "Stage 1", 36f, RunOverlayUI.Gold, UIHouseStyle.Title);
        stageLabel.alignment = TextAlignmentOptions.MidlineLeft;
        stageLabel.enableAutoSizing = true;
        stageLabel.fontSizeMin = 20f;
        stageLabel.fontSizeMax = 36f;
        stageLabel.rectTransform.anchorMin = Vector2.zero;
        stageLabel.rectTransform.anchorMax = new Vector2(0.2f, 1f);
        stageLabel.rectTransform.offsetMin = new Vector2(30f, 0f);
        stageLabel.rectTransform.offsetMax = Vector2.zero;

        var goal = Label("Goal", rect, "Finish 4 runs\n0/4", 32f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        goal.alignment = TextAlignmentOptions.MidlineLeft;
        goal.enableAutoSizing = true;
        goal.fontSizeMin = 18f;
        goal.fontSizeMax = 32f;
        goal.rectTransform.anchorMin = new Vector2(0.2f, 0f);
        goal.rectTransform.anchorMax = new Vector2(0.78f, 1f);
        goal.rectTransform.offsetMin = new Vector2(10f, 8f);
        goal.rectTransform.offsetMax = new Vector2(0f, -8f);

        var reward = Label("Reward", rect, "+15 jade", 32f, RunOverlayUI.Gold, UIHouseStyle.Body);
        reward.alignment = TextAlignmentOptions.MidlineRight;
        reward.enableAutoSizing = true;
        reward.fontSizeMin = 18f;
        reward.fontSizeMax = 32f;
        reward.rectTransform.anchorMin = new Vector2(0.78f, 0f);
        reward.rectTransform.anchorMax = Vector2.one;
        reward.rectTransform.offsetMin = new Vector2(0f, 8f);
        reward.rectTransform.offsetMax = new Vector2(-24f, -8f);

        var so = new SerializedObject(row);
        so.FindProperty("stageLabel").objectReferenceValue = stageLabel;
        so.FindProperty("goalLabel").objectReferenceValue = goal;
        so.FindProperty("rewardLabel").objectReferenceValue = reward;
        so.FindProperty("doneMarker").objectReferenceValue = marker.gameObject;
        so.FindProperty("group").objectReferenceValue = rect.GetComponent<CanvasGroup>();
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    /// <summary>False when the prefab's row template has no resolvable ExpeditionRow when opened for editing.</summary>
    private static bool RowResolves()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { return root.GetComponentInChildren<ExpeditionRow>(true) != null; }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, string role)
    {
        var label = RunOverlayUI.CreateLabel(name, parent, text, size, color);
        UIHouseStyle.ApplyLabel(label, role);
        label.color = color;
        label.fontSize = size;
        return label;
    }

    private static string EnsureChip()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath) == null)
        {
            string missing = MainMenuPrefabPath + " was not found.";
            Debug.LogError($"[ExpeditionUISetup] {missing}");
            return missing;
        }

        string result = null;
        GameObject root = PrefabUtility.LoadPrefabContents(MainMenuPrefabPath);
        try
        {
            result = BuildChip(root);
            if (result == null)
            {
                PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
                result = $"Added {ChipName} to {MainMenuPrefabPath}.";
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[ExpeditionUISetup] {result}");
        return result;
    }

    /// <returns>Null on success, or the reason nothing was built.</returns>
    private static string BuildChip(GameObject root)
    {
        var menu = root.GetComponentInChildren<MainMenuManager>(true);
        var panel = menu != null
            ? new SerializedObject(menu).FindProperty("mainMenuPanel").objectReferenceValue as GameObject
            : null;
        if (panel == null) return "The prefab has no Main Menu panel (MainMenuManager.mainMenuPanel is unassigned).";
        if (panel.transform.Find(ChipName) != null) return $"Kept existing {ChipName}.";

        var chip = RunOverlayUI.CreateChild(ChipName, panel.transform);
        chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f);
        chip.pivot = new Vector2(1f, 1f);
        chip.anchoredPosition = ChipPosition;
        chip.sizeDelta = ChipSize;
        chip.SetAsLastSibling();

        var image = chip.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(image, UIHouseStyle.Secondary);
        var button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UIHouseStyle.ApplyButton(button, UIHouseStyle.Secondary);

        var title = Label("Label", chip, "EXPEDITION", 30f, RunOverlayUI.Gold, UIHouseStyle.Title);
        title.enableAutoSizing = true;
        title.fontSizeMin = 14f;
        title.fontSizeMax = 30f;
        title.enableWordWrapping = false;
        title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(8f, 0f);
        title.rectTransform.offsetMax = new Vector2(-8f, -8f);
        var localized = title.gameObject.AddComponent<LocalizedText>();
        var localizedSo = new SerializedObject(localized);
        var keyProp = localizedSo.FindProperty("localizationKey");
        if (keyProp != null)
        {
            keyProp.stringValue = "expedition_chip";
            localizedSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var count = Label("Count", chip, "0/5", 28f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        count.enableAutoSizing = true;
        count.fontSizeMin = 16f;
        count.fontSizeMax = 30f;
        count.enableWordWrapping = false;
        count.rectTransform.anchorMin = new Vector2(0f, 0f);
        count.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        count.rectTransform.offsetMin = new Vector2(8f, 8f);
        count.rectTransform.offsetMax = new Vector2(-8f, 0f);

        chip.gameObject.AddComponent<CanvasGroup>();
        var chipView = chip.gameObject.AddComponent<ExpeditionMenuChip>();
        var so = new SerializedObject(chipView);
        so.FindProperty("countLabel").objectReferenceValue = count;
        so.ApplyModifiedPropertiesWithoutUndo();
        return null;
    }
}
#endif
