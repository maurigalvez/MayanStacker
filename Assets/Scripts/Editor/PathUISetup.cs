#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the Path of Kukulkan panel:
///   • Resources/UI/Path.prefab - tier, XP bar and the scrolling tier list (<see cref="PathView"/>),
///     styled through <see cref="UIHouseStyle"/>. Kept if it already exists, so hand styling survives a re-run.
///   • a "PATH" chip (<see cref="PathMenuChip"/>) on the Main Menu panel inside the Main Menu Canvas
///     prefab, under the Login Chest chip. Skipped if one already exists.
///
/// Menus: TamalStacker ▸ UI ▸ Create Path Prefab, TamalStacker ▸ Economy ▸ Add Path Menu Chip.
/// </summary>
public static class PathUISetup
{
    private const string PrefabPath = "Assets/Resources/UI/Path.prefab";
    private const string MainMenuPrefabPath = "Assets/Prefabs/Main Menu/Main Menu Canvas.prefab";
    private const string ChipName = "PathChip";

    // Directly under the Login Chest chip (-640, 120 tall) with a 20 px gap.
    private static readonly Vector2 ChipPosition = new Vector2(-30f, -780f);
    private static readonly Vector2 ChipSize = new Vector2(180f, 120f);

    [MenuItem("TamalStacker/UI/Create Path Prefab")]
    public static void CreatePrefab()
    {
        EditorUtility.DisplayDialog("Path of Kukulkan", EnsurePrefab(), "OK");
    }

    [MenuItem("TamalStacker/Economy/Add Path Menu Chip")]
    public static void AddMenuChip()
    {
        EditorUtility.DisplayDialog("Path of Kukulkan", EnsureChip(), "OK");
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
        Debug.Log($"[PathUISetup] {note}");
        return note;
    }

    private static GameObject BuildPanel()
    {
        var root = new GameObject("Path",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PathView));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = PathView.SortingOrder;

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
        RunOverlayUI.Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1560f));
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;
        UIHouseStyle.ApplyImage(slab, UIHouseStyle.Modal);

        var title = Label("Title", modal, "Path of Kukulkan", 60f, RunOverlayUI.Gold, UIHouseStyle.Title);
        RunOverlayUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(800f, 90f));

        var tier = Label("Tier", modal, "Tier 0/30", 44f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        RunOverlayUI.Place(tier.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(800f, 60f));

        // XP bar: dark track, jade fill whose right anchor the view moves, XP text on top.
        var track = RunOverlayUI.CreateChild("BarTrack", modal);
        RunOverlayUI.Place(track, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(800f, 56f));
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

        var barLabel = Label("BarLabel", track, "0/60 XP", 30f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        RunOverlayUI.Stretch(barLabel.rectTransform);

        // ── Scroll list ──
        var scroll = RunOverlayUI.CreateChild("Scroll", modal);
        scroll.anchorMin = Vector2.zero;
        scroll.anchorMax = Vector2.one;
        scroll.offsetMin = new Vector2(50f, 230f);
        scroll.offsetMax = new Vector2(-50f, -300f);
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();

        var viewport = RunOverlayUI.CreateChild("Viewport", scroll);
        RunOverlayUI.Stretch(viewport);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = RunOverlayUI.CreateChild("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 40f;

        var row = BuildRow(scroll);

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

        var so = new SerializedObject(root.GetComponent<PathView>());
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("tierLabel").objectReferenceValue = tier;
        so.FindProperty("barFill").objectReferenceValue = fill;
        so.FindProperty("barLabel").objectReferenceValue = barLabel;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("closeLabel").objectReferenceValue = closeLabel;
        so.FindProperty("scroll").objectReferenceValue = scrollRect;
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("rowTemplate").objectReferenceValue = row;
        so.ApplyModifiedPropertiesWithoutUndo();
        SpinsPanelStyle.Dress(root);
        return root;
    }

    private static PathRow BuildRow(Transform parent)
    {
        var rect = RunOverlayUI.CreateChild("RowTemplate", parent);
        rect.sizeDelta = new Vector2(840f, 130f);
        var bg = rect.gameObject.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.07f);
        bg.raycastTarget = false;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 130f;
        rect.gameObject.AddComponent<CanvasGroup>();
        var row = rect.gameObject.AddComponent<PathRow>();

        // Gold edge on the left marks a reached tier.
        var marker = RunOverlayUI.CreateChild("ReachedMarker", rect);
        marker.anchorMin = new Vector2(0f, 0f);
        marker.anchorMax = new Vector2(0f, 1f);
        marker.pivot = new Vector2(0f, 0.5f);
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = new Vector2(10f, 0f);
        var markerImage = marker.gameObject.AddComponent<Image>();
        markerImage.color = RunOverlayUI.Gold;
        markerImage.raycastTarget = false;

        var tierLabel = Label("Tier", rect, "Tier 1", 40f, RunOverlayUI.Gold, UIHouseStyle.Title);
        tierLabel.alignment = TextAlignmentOptions.MidlineLeft;
        tierLabel.rectTransform.anchorMin = Vector2.zero;
        tierLabel.rectTransform.anchorMax = new Vector2(0.4f, 1f);
        tierLabel.rectTransform.offsetMin = new Vector2(40f, 0f);
        tierLabel.rectTransform.offsetMax = new Vector2(0f, 0f);

        var reward = Label("Reward", rect, "+10 jade", 32f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        reward.alignment = TextAlignmentOptions.MidlineRight;
        reward.enableAutoSizing = true;
        reward.fontSizeMin = 18f;
        reward.fontSizeMax = 32f;
        reward.rectTransform.anchorMin = new Vector2(0.4f, 0f);
        reward.rectTransform.anchorMax = Vector2.one;
        reward.rectTransform.offsetMin = new Vector2(0f, 8f);
        reward.rectTransform.offsetMax = new Vector2(-30f, -8f);

        var so = new SerializedObject(row);
        so.FindProperty("tierLabel").objectReferenceValue = tierLabel;
        so.FindProperty("rewardLabel").objectReferenceValue = reward;
        so.FindProperty("reachedMarker").objectReferenceValue = marker.gameObject;
        so.FindProperty("group").objectReferenceValue = rect.GetComponent<CanvasGroup>();
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    /// <summary>False when the prefab's row template has no resolvable PathRow when opened for editing.</summary>
    private static bool RowResolves()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { return root.GetComponentInChildren<PathRow>(true) != null; }
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
            Debug.LogError($"[PathUISetup] {missing}");
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

        Debug.Log($"[PathUISetup] {result}");
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

        var title = Label("Label", chip, "PATH", 30f, RunOverlayUI.Gold, UIHouseStyle.Title);
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
            keyProp.stringValue = "path_chip";
            localizedSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var count = Label("Count", chip, "Tier 0/30", 28f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        count.enableAutoSizing = true;
        count.fontSizeMin = 16f;
        count.fontSizeMax = 30f;
        count.enableWordWrapping = false;
        count.rectTransform.anchorMin = new Vector2(0f, 0f);
        count.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        count.rectTransform.offsetMin = new Vector2(8f, 8f);
        count.rectTransform.offsetMax = new Vector2(-8f, 0f);

        chip.gameObject.AddComponent<CanvasGroup>();
        var chipView = chip.gameObject.AddComponent<PathMenuChip>();
        var so = new SerializedObject(chipView);
        so.FindProperty("countLabel").objectReferenceValue = count;
        so.ApplyModifiedPropertiesWithoutUndo();
        return null;
    }
}
#endif
