#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the Jade Market:
///   • Resources/UI/JadeMarket.prefab - the shop panel (<see cref="JadeMarketView"/>), styled through
///     <see cref="UIHouseStyle"/>. Kept if it already exists, so hand styling survives a re-run.
///   • a "MARKET" chip (<see cref="JadeMarketMenuChip"/>) on the Main Menu panel inside the Main Menu
///     Canvas prefab, under the Remove Ads chip. Skipped if one already exists.
///
/// Menus: TamalStacker ▸ UI ▸ Create Jade Market Prefab, TamalStacker ▸ Economy ▸ Add Jade Market Menu Chip.
/// </summary>
public static class JadeMarketSetup
{
    private const string PrefabPath = "Assets/Resources/UI/JadeMarket.prefab";
    private const string MainMenuPrefabPath = "Assets/Prefabs/Main Menu/Main Menu Canvas.prefab";
    private const string ChipName = "JadeMarketChip";

    private static readonly Vector2 ChipPosition = new Vector2(-30f, -360f);
    private static readonly Vector2 ChipSize = new Vector2(180f, 120f);

    [MenuItem("TamalStacker/UI/Create Jade Market Prefab")]
    public static void CreatePrefab()
    {
        EditorUtility.DisplayDialog("Jade Market", EnsurePrefab(), "OK");
    }

    [MenuItem("TamalStacker/Economy/Add Jade Market Menu Chip")]
    public static void AddMenuChip()
    {
        EditorUtility.DisplayDialog("Jade Market", EnsureChip(), "OK");
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
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            note = $"Kept {PrefabPath}.";
        }
        else
        {
            GameObject root = BuildMarket();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            note = $"Created {PrefabPath}.";
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[JadeMarketSetup] {note}");
        return note;
    }

    private static GameObject BuildMarket()
    {
        var root = new GameObject("JadeMarket",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(JadeMarketView));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = JadeMarketView.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // The panel holds the dimmed backdrop as a child so showing/hiding it is one object.
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

        var title = Label("Title", modal, "Jade Market", 60f, RunOverlayUI.Gold, UIHouseStyle.Title);
        RunOverlayUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(800f, 90f));

        var balance = Label("Balance", modal, "0 jade", 40f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        RunOverlayUI.Place(balance.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(800f, 60f));

        // ── Scroll list ──
        var scroll = RunOverlayUI.CreateChild("Scroll", modal);
        scroll.anchorMin = Vector2.zero;
        scroll.anchorMax = Vector2.one;
        scroll.offsetMin = new Vector2(50f, 230f);
        scroll.offsetMax = new Vector2(-50f, -230f);
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
        layout.spacing = 14f;
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

        // ── Templates (cloned at runtime, hidden by the view) ──
        var header = Label("HeaderTemplate", scroll, "Section", 40f, RunOverlayUI.Gold, UIHouseStyle.Title);
        header.alignment = TextAlignmentOptions.MidlineLeft;
        header.rectTransform.sizeDelta = new Vector2(800f, 80f);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 80f;

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

        var so = new SerializedObject(root.GetComponent<JadeMarketView>());
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("balanceLabel").objectReferenceValue = balance;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("closeLabel").objectReferenceValue = closeLabel;
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("rowTemplate").objectReferenceValue = row;
        so.FindProperty("headerTemplate").objectReferenceValue = header;
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static JadeMarketRow BuildRow(Transform parent)
    {
        var rect = RunOverlayUI.CreateChild("RowTemplate", parent);
        rect.sizeDelta = new Vector2(840f, 130f);
        var bg = rect.gameObject.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.07f);
        bg.raycastTarget = false;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 130f;
        var row = rect.gameObject.AddComponent<JadeMarketRow>();

        var marker = RunOverlayUI.CreateChild("WornMarker", rect);
        marker.anchorMin = new Vector2(0f, 0f);
        marker.anchorMax = new Vector2(0f, 1f);
        marker.pivot = new Vector2(0f, 0.5f);
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = new Vector2(10f, 0f);
        var markerImage = marker.gameObject.AddComponent<Image>();
        markerImage.color = RunOverlayUI.Gold;
        markerImage.raycastTarget = false;

        var swatch = Swatch("Swatch", rect, 40f);
        var swatch2 = Swatch("Swatch2", rect, 100f);

        var name = Label("Name", rect, "Item", 40f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        name.alignment = TextAlignmentOptions.MidlineLeft;
        name.enableAutoSizing = true;
        name.fontSizeMin = 24f;
        name.fontSizeMax = 40f;
        name.rectTransform.anchorMin = new Vector2(0f, 0f);
        name.rectTransform.anchorMax = new Vector2(1f, 1f);
        name.rectTransform.offsetMin = new Vector2(180f, 10f);
        name.rectTransform.offsetMax = new Vector2(-290f, -10f);

        var action = RunOverlayUI.CreateChild("ActionButton", rect);
        action.anchorMin = action.anchorMax = new Vector2(1f, 0.5f);
        action.pivot = new Vector2(1f, 0.5f);
        action.anchoredPosition = new Vector2(-20f, 0f);
        action.sizeDelta = new Vector2(260f, 96f);
        var actionImage = action.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(actionImage, UIHouseStyle.Primary);
        var actionButton = action.gameObject.AddComponent<Button>();
        actionButton.targetGraphic = actionImage;
        UIHouseStyle.ApplyButton(actionButton, UIHouseStyle.Primary);
        var actionLabel = Label("Label", action, "Buy", 34f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        actionLabel.enableAutoSizing = true;
        actionLabel.fontSizeMin = 20f;
        actionLabel.fontSizeMax = 34f;
        RunOverlayUI.Stretch(actionLabel.rectTransform);

        var so = new SerializedObject(row);
        so.FindProperty("swatch").objectReferenceValue = swatch;
        so.FindProperty("swatch2").objectReferenceValue = swatch2;
        so.FindProperty("nameLabel").objectReferenceValue = name;
        so.FindProperty("actionButton").objectReferenceValue = actionButton;
        so.FindProperty("actionLabel").objectReferenceValue = actionLabel;
        so.FindProperty("wornMarker").objectReferenceValue = markerImage;
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    private static Image Swatch(string name, Transform parent, float x)
    {
        var rect = RunOverlayUI.CreateChild(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(60f, 90f);
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
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
            Debug.LogError($"[JadeMarketSetup] {missing}");
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

        Debug.Log($"[JadeMarketSetup] {result}");
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

        var title = Label("Label", chip, "MARKET", 30f, RunOverlayUI.Gold, UIHouseStyle.Title);
        title.enableAutoSizing = true;
        title.fontSizeMin = 20f;
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
            keyProp.stringValue = "market_chip";
            localizedSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var balance = Label("Balance", chip, "0 jade", 28f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        balance.enableAutoSizing = true;
        balance.fontSizeMin = 18f;
        balance.fontSizeMax = 30f;
        balance.enableWordWrapping = false;
        balance.rectTransform.anchorMin = new Vector2(0f, 0f);
        balance.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        balance.rectTransform.offsetMin = new Vector2(8f, 8f);
        balance.rectTransform.offsetMax = new Vector2(-8f, 0f);

        chip.gameObject.AddComponent<CanvasGroup>();
        var chipView = chip.gameObject.AddComponent<JadeMarketMenuChip>();
        var so = new SerializedObject(chipView);
        so.FindProperty("balanceLabel").objectReferenceValue = balance;
        so.ApplyModifiedPropertiesWithoutUndo();
        return null;
    }
}
#endif
