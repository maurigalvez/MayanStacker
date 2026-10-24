#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the Daily Missions panel:
///   • Resources/UI/Missions.prefab - the three-row panel (<see cref="MissionsView"/>), styled through
///     <see cref="UIHouseStyle"/>. Kept if it already exists, so hand styling survives a re-run.
///   • a "MISSIONS" chip (<see cref="MissionsMenuChip"/>) on the Main Menu panel inside the Main Menu
///     Canvas prefab, under the Market chip. Skipped if one already exists.
///
/// Menus: TamalStacker ▸ UI ▸ Create Missions Prefab, TamalStacker ▸ Economy ▸ Add Missions Menu Chip.
/// </summary>
public static class MissionsSetup
{
    private const string PrefabPath = "Assets/Resources/UI/Missions.prefab";
    private const string MainMenuPrefabPath = "Assets/Prefabs/Main Menu/Main Menu Canvas.prefab";
    private const string ChipName = "MissionsChip";

    // Directly under the Market chip (-360, 120 tall) with a 20 px gap.
    private static readonly Vector2 ChipPosition = new Vector2(-30f, -500f);
    private static readonly Vector2 ChipSize = new Vector2(180f, 120f);

    [MenuItem("TamalStacker/UI/Create Missions Prefab")]
    public static void CreatePrefab()
    {
        EditorUtility.DisplayDialog("Missions", EnsurePrefab(), "OK");
    }

    [MenuItem("TamalStacker/Economy/Add Missions Menu Chip")]
    public static void AddMenuChip()
    {
        EditorUtility.DisplayDialog("Missions", EnsureChip(), "OK");
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
        bool broken = existing != null && !RowResolves();
        if (existing != null && !broken)
        {
            note = $"Kept {PrefabPath}." + EnsureRerollButton();
        }
        else
        {
            GameObject root = BuildPanel();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            // A prefab whose row lost its MissionRow script (the class once lived in the wrong file) is
            // unusable, so it is rebuilt in place; the asset keeps its GUID.
            note = broken ? $"Rebuilt {PrefabPath} (its row script was missing)." : $"Created {PrefabPath}.";
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MissionsSetup] {note}");
        return note;
    }

    private static GameObject BuildPanel()
    {
        var root = new GameObject("Missions",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MissionsView));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = MissionsView.SortingOrder;

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
        RunOverlayUI.Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1080f));
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;
        UIHouseStyle.ApplyImage(slab, UIHouseStyle.Modal);

        var title = Label("Title", modal, "Daily Missions", 60f, RunOverlayUI.Gold, UIHouseStyle.Title);
        RunOverlayUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(800f, 90f));

        var count = Label("Count", modal, "0/3 done", 40f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        RunOverlayUI.Place(count.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(800f, 60f));

        var content = RunOverlayUI.CreateChild("Content", modal);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(50f, 230f);
        content.offsetMax = new Vector2(-50f, -230f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var row = BuildRow(modal);

        var close = RunOverlayUI.CreateChild("CloseButton", modal);
        RunOverlayUI.Place(close, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(520f, 110f));
        var closeImage = close.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(closeImage, UIHouseStyle.Secondary);
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        UIHouseStyle.ApplyButton(closeButton, UIHouseStyle.Secondary);
        var closeLabel = Label("Label", close, "Close", 40f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        RunOverlayUI.Stretch(closeLabel.rectTransform);

        var so = new SerializedObject(root.GetComponent<MissionsView>());
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("countLabel").objectReferenceValue = count;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("closeLabel").objectReferenceValue = closeLabel;
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("rowTemplate").objectReferenceValue = row;
        so.ApplyModifiedPropertiesWithoutUndo();
        SpinsPanelStyle.Dress(root);
        return root;
    }

    private static MissionRow BuildRow(Transform parent)
    {
        var rect = RunOverlayUI.CreateChild("RowTemplate", parent);
        rect.sizeDelta = new Vector2(840f, 200f);
        var bg = rect.gameObject.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.07f);
        bg.raycastTarget = false;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 200f;
        var row = rect.gameObject.AddComponent<MissionRow>();

        // Gold edge on the left marks a finished mission.
        var marker = RunOverlayUI.CreateChild("DoneMarker", rect);
        marker.anchorMin = new Vector2(0f, 0f);
        marker.anchorMax = new Vector2(0f, 1f);
        marker.pivot = new Vector2(0f, 0.5f);
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = new Vector2(10f, 0f);
        var markerImage = marker.gameObject.AddComponent<Image>();
        markerImage.color = RunOverlayUI.Gold;
        markerImage.raycastTarget = false;

        var description = Label("Description", rect, "Goal", 38f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        description.alignment = TextAlignmentOptions.MidlineLeft;
        description.enableAutoSizing = true;
        description.fontSizeMin = 24f;
        description.fontSizeMax = 38f;
        description.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        description.rectTransform.anchorMax = new Vector2(1f, 1f);
        description.rectTransform.offsetMin = new Vector2(40f, 0f);
        description.rectTransform.offsetMax = new Vector2(-240f, -16f);

        var reward = Label("Reward", rect, "+8 jade", 34f, RunOverlayUI.Gold, UIHouseStyle.Title);
        reward.alignment = TextAlignmentOptions.MidlineRight;
        reward.enableAutoSizing = true;
        reward.fontSizeMin = 20f;
        reward.fontSizeMax = 34f;
        reward.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        reward.rectTransform.anchorMax = new Vector2(1f, 1f);
        reward.rectTransform.pivot = new Vector2(1f, 1f);
        reward.rectTransform.anchoredPosition = new Vector2(-30f, -16f);
        reward.rectTransform.sizeDelta = new Vector2(200f, 0f);

        // Bar: dark track, jade fill whose right anchor the row moves, progress text on top.
        var track = RunOverlayUI.CreateChild("BarTrack", rect);
        track.anchorMin = new Vector2(0f, 0f);
        track.anchorMax = new Vector2(1f, 0f);
        track.pivot = new Vector2(0.5f, 0f);
        track.anchoredPosition = new Vector2(0f, 24f);
        track.sizeDelta = new Vector2(-80f, 60f);
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0f, 0f, 0f, 0.45f);
        trackImage.raycastTarget = false;

        var fill = RunOverlayUI.CreateChild("BarFill", track);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.35f, 0.85f, 0.6f, 1f);
        fillImage.raycastTarget = false;

        var progress = Label("Progress", track, "0/3", 30f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        RunOverlayUI.Stretch(progress.rectTransform);

        var so = new SerializedObject(row);
        so.FindProperty("descriptionLabel").objectReferenceValue = description;
        so.FindProperty("progressLabel").objectReferenceValue = progress;
        so.FindProperty("rewardLabel").objectReferenceValue = reward;
        so.FindProperty("barFill").objectReferenceValue = fill;
        so.FindProperty("doneMarker").objectReferenceValue = marker.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        AddRerollButton(row);
        return row;
    }

    /// <summary>
    /// Adds the swap button to a row template and wires it. It sits left of the reward text, so the
    /// description gives up that width. Skipped if the row already has one.
    /// </summary>
    private static void AddRerollButton(MissionRow row)
    {
        var so = new SerializedObject(row);
        if (so.FindProperty("rerollButton").objectReferenceValue != null) return;

        var rect = (RectTransform)row.transform;
        var swap = RunOverlayUI.CreateChild("RerollButton", rect);
        swap.anchorMin = swap.anchorMax = new Vector2(1f, 1f);
        swap.pivot = new Vector2(1f, 1f);
        swap.anchoredPosition = new Vector2(-250f, -14f);
        swap.sizeDelta = new Vector2(220f, 70f);
        var image = swap.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(image, UIHouseStyle.Secondary);
        var button = swap.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UIHouseStyle.ApplyButton(button, UIHouseStyle.Secondary);
        var label = Label("Label", swap, "Swap", 28f, RunOverlayUI.Gold, UIHouseStyle.Body);
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = 28f;
        label.enableWordWrapping = false;
        RunOverlayUI.Stretch(label.rectTransform);
        swap.gameObject.SetActive(false);

        var description = so.FindProperty("descriptionLabel").objectReferenceValue as TextMeshProUGUI;
        if (description != null) description.rectTransform.offsetMax = new Vector2(-490f, -16f);

        so.FindProperty("rerollButton").objectReferenceValue = button;
        so.FindProperty("rerollLabel").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// False when the prefab's row template has no resolvable MissionRow when opened for editing. A
    /// prefab saved while the class sat in a differently named file stores a script-less reference
    /// that the asset view tolerates but an edit session does not.
    /// </summary>
    private static bool RowResolves()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { return root.GetComponentInChildren<MissionRow>(true) != null; }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Adds the swap button to an existing Missions prefab without touching its other styling.</summary>
    private static string EnsureRerollButton()
    {        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var row = root.GetComponentInChildren<MissionRow>(true);
            if (row == null) return " No row template to add the swap button to.";
            if (new SerializedObject(row).FindProperty("rerollButton").objectReferenceValue != null)
                return " Swap button already present.";

            AddRerollButton(row);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return " Added the swap button to the row template.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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
            Debug.LogError($"[MissionsSetup] {missing}");
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

        Debug.Log($"[MissionsSetup] {result}");
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

        var title = Label("Label", chip, "MISSIONS", 30f, RunOverlayUI.Gold, UIHouseStyle.Title);
        title.enableAutoSizing = true;
        title.fontSizeMin = 18f;
        title.fontSizeMax = 32f;
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
            keyProp.stringValue = "missions_chip";
            localizedSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var count = Label("Count", chip, "0/3 done", 28f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        count.enableAutoSizing = true;
        count.fontSizeMin = 16f;
        count.fontSizeMax = 30f;
        count.enableWordWrapping = false;
        count.rectTransform.anchorMin = new Vector2(0f, 0f);
        count.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        count.rectTransform.offsetMin = new Vector2(8f, 8f);
        count.rectTransform.offsetMax = new Vector2(-8f, 0f);

        chip.gameObject.AddComponent<CanvasGroup>();
        var chipView = chip.gameObject.AddComponent<MissionsMenuChip>();
        var so = new SerializedObject(chipView);
        so.FindProperty("countLabel").objectReferenceValue = count;
        so.ApplyModifiedPropertiesWithoutUndo();
        return null;
    }
}
#endif
