#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates Assets/Resources/UI/ReleaseAnnouncement.prefab - the "what's new" card returning players see
/// once per release (<see cref="ReleaseAnnouncementView"/>). Styled through <see cref="UIHouseStyle"/>;
/// each highlight row wears its feature's rail medallion from the Main Menu Rail art, and the thank-you
/// block carries the jade emblem. Kept if it exists, so hand styling survives a re-run; delete the
/// prefab to rebuild it after the copy or the feature list changes.
///
/// Refuses to build while any of those icons is unassigned in the Main Menu Rail art asset - the card
/// never ships with a stand-in.
///
/// Menu: TamalStacker ▸ UI ▸ Create Release Announcement Prefab
/// </summary>
public static class ReleaseAnnouncementSetup
{
    private const string PrefabPath = "Assets/Resources/UI/ReleaseAnnouncement.prefab";

    private const float ModalWidth = 900f;
    private const float ModalHeight = 1640f;
    private const float RowHeight = 160f;
    private const float RowPitch = 165f;
    private const float RowsTop = 185f;
    private const float IconSize = 110f;
    private const float TextLeft = 150f;

    [MenuItem("TamalStacker/UI/Create Release Announcement Prefab")]
    public static void CreatePrefab()
    {
        EditorUtility.DisplayDialog("Release announcement", EnsurePrefab(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static void CreateBatch() => EnsurePrefab();

    private static string EnsurePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return $"Kept {PrefabPath}. Delete it to rebuild.";

        var art = MainMenuRailSetup.LoadArt();
        string missing = MissingArt(art);
        if (missing != null)
        {
            Debug.LogError($"[ReleaseAnnouncementSetup] {missing}");
            return missing;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");

        GameObject root = Build(art);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string note = $"Created {PrefabPath}.";
        Debug.Log($"[ReleaseAnnouncementSetup] {note}");
        return note;
    }

    private static string MissingArt(MenuRailArt art)
    {
        if (art == null) return "The Main Menu Rail art asset was not found - run TamalStacker > UI > Main Menu Rail > Select Art Slots first.";
        var gaps = new System.Collections.Generic.List<string>();
        if (art.market == null) gaps.Add("market");
        if (art.chest == null) gaps.Add("chest");
        if (art.missions == null) gaps.Add("missions");
        if (art.path == null) gaps.Add("path");
        if (art.expedition == null) gaps.Add("expedition");
        if (art.jade == null) gaps.Add("jade");
        return gaps.Count == 0 ? null
            : "The release card needs these icons in the Main Menu Rail art, still empty: " + string.Join(", ", gaps) + ".";
    }

    private static GameObject Build(MenuRailArt art)
    {
        var root = new GameObject("ReleaseAnnouncement",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ReleaseAnnouncementView));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ReleaseAnnouncementView.SortingOrder;

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
        RunOverlayUI.Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ModalWidth, ModalHeight));
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;
        UIHouseStyle.ApplyImage(slab, UIHouseStyle.Modal);
        float inner = ModalWidth - PanelContentFit.SideInset * 2f;

        var title = Label("Title", modal, "What's New in 1.6.0", 56f, RunOverlayUI.Gold, UIHouseStyle.Title, TextAlignmentOptions.Center);
        FitOneLine(title, 56f);
        RunOverlayUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(inner, 90f));

        Sprite rowSprite = SpinsPanelStyle.Load(SpinsPanelStyle.LeaderboardSheet, SpinsPanelStyle.RowSprite);
        Sprite[] icons = { art.market, art.chest, art.missions, art.path, art.expedition };
        string[] names = { "Jade Market", "Kukulkan's Chest", "Daily Missions", "Path of Kukulkan", "Weekly Expedition" };
        string[] lines = { "Spend jade on sky themes and looks.", "A free jade gift every day.", "Finish missions to earn jade.",
                           "Every run earns XP toward rewards.", "Five stages a week that pay jade." };
        var rowTitles = new TextMeshProUGUI[icons.Length];
        var rowBodies = new TextMeshProUGUI[icons.Length];
        for (int i = 0; i < icons.Length; i++)
        {
            var row = RunOverlayUI.CreateChild("Row" + (i + 1), modal);
            RunOverlayUI.Place(row, new Vector2(0.5f, 1f), new Vector2(0f, -(RowsTop + RowHeight * 0.5f + i * RowPitch)), new Vector2(inner, RowHeight));
            var bg = row.gameObject.AddComponent<Image>();
            SpinsPanelStyle.Slice(bg, rowSprite, SpinsPanelStyle.RowSliceScale, Color.white);

            var icon = RunOverlayUI.CreateChild("Icon", row);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(IconSize, IconSize);
            icon.anchoredPosition = new Vector2(20f + IconSize * 0.5f, 0f);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = icons[i];
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            rowTitles[i] = Label("Name", row, names[i], 40f, RunOverlayUI.Gold, UIHouseStyle.Title, TextAlignmentOptions.Left);
            FitOneLine(rowTitles[i], 40f);
            Anchor(rowTitles[i].rectTransform, top: true, height: 52f);

            rowBodies[i] = Label("Line", row, lines[i], 35f, RunOverlayUI.Parchment, UIHouseStyle.Body, TextAlignmentOptions.TopLeft);
            rowBodies[i].enableAutoSizing = true;
            rowBodies[i].fontSizeMin = UIHouseStyle.MinFontSize;
            rowBodies[i].fontSizeMax = 35f;
            rowBodies[i].enableWordWrapping = true;
            Anchor(rowBodies[i].rectTransform, top: false, height: RowHeight - 62f);
        }

        // ── Thank-you block with the chest bonus ──
        float bonusTop = RowsTop + 5 * RowPitch + 15f;
        const float bonusHeight = 290f;
        var bonus = RunOverlayUI.CreateChild("BonusBlock", modal);
        RunOverlayUI.Place(bonus, new Vector2(0.5f, 1f), new Vector2(0f, -(bonusTop + bonusHeight * 0.5f)), new Vector2(inner, bonusHeight));
        var bonusBg = bonus.gameObject.AddComponent<Image>();
        SpinsPanelStyle.Slice(bonusBg, SpinsPanelStyle.Load(SpinsPanelStyle.LeaderboardSheet, SpinsPanelStyle.HighlightSprite),
            SpinsPanelStyle.RowSliceScale, Color.white);

        var emblem = RunOverlayUI.CreateChild("Emblem", bonus);
        emblem.anchorMin = emblem.anchorMax = new Vector2(0f, 0.5f);
        emblem.pivot = new Vector2(0.5f, 0.5f);
        emblem.sizeDelta = new Vector2(130f, 130f / 1.53f);
        emblem.anchoredPosition = new Vector2(20f + 65f, 0f);
        var emblemImage = emblem.gameObject.AddComponent<Image>();
        emblemImage.sprite = art.jade;
        emblemImage.preserveAspect = true;
        emblemImage.raycastTarget = false;

        var bonusTitle = Label("BonusTitle", bonus, "Thank you for stacking!", 42f, RunOverlayUI.Gold, UIHouseStyle.Title, TextAlignmentOptions.TopLeft);
        bonusTitle.enableAutoSizing = true;
        bonusTitle.fontSizeMin = UIHouseStyle.MinFontSize;
        bonusTitle.fontSizeMax = 42f;
        bonusTitle.enableWordWrapping = true;
        var bt = bonusTitle.rectTransform;
        bt.anchorMin = new Vector2(0f, 0.5f); bt.anchorMax = new Vector2(1f, 1f);
        bt.offsetMin = new Vector2(TextLeft + 20f, 0f); bt.offsetMax = new Vector2(-24f, -20f);

        var bonusBody = Label("BonusBody", bonus, "Every chest you open in the next 7 days pays 2x jade.", 35f, RunOverlayUI.Parchment, UIHouseStyle.Body, TextAlignmentOptions.TopLeft);
        bonusBody.enableAutoSizing = true;
        bonusBody.fontSizeMin = UIHouseStyle.MinFontSize;
        bonusBody.fontSizeMax = 35f;
        bonusBody.enableWordWrapping = true;
        var bb = bonusBody.rectTransform;
        bb.anchorMin = new Vector2(0f, 0f); bb.anchorMax = new Vector2(1f, 0.5f);
        bb.offsetMin = new Vector2(TextLeft + 20f, 20f); bb.offsetMax = new Vector2(-24f, 0f);

        // ── Buttons ──
        var primary = Button("PrimaryButton", modal, "Open the Chest", UIHouseStyle.Primary, UIHouseStyle.Title, 46f, out var primaryLabel);
        RunOverlayUI.Place(primary, new Vector2(0.5f, 0f), new Vector2(0f, 205f), new Vector2(620f, 120f));
        var secondary = Button("SecondaryButton", modal, "Later", UIHouseStyle.Secondary, UIHouseStyle.Body, 38f, out var secondaryLabel);
        RunOverlayUI.Place(secondary, new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(420f, 100f));

        var so = new SerializedObject(root.GetComponent<ReleaseAnnouncementView>());
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        Assign(so.FindProperty("rowTitles"), rowTitles);
        Assign(so.FindProperty("rowBodies"), rowBodies);
        so.FindProperty("bonusBlock").objectReferenceValue = bonus.gameObject;
        so.FindProperty("bonusTitle").objectReferenceValue = bonusTitle;
        so.FindProperty("bonusBody").objectReferenceValue = bonusBody;
        so.FindProperty("primaryButton").objectReferenceValue = primary.GetComponent<UnityEngine.UI.Button>();
        so.FindProperty("primaryLabel").objectReferenceValue = primaryLabel;
        so.FindProperty("secondaryButton").objectReferenceValue = secondary.GetComponent<UnityEngine.UI.Button>();
        so.FindProperty("secondaryLabel").objectReferenceValue = secondaryLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        modal.gameObject.AddComponent<ModalFit>();
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) UIHouseStyle.EnforceMinFontSize(label);
        return root;
    }

    private static void Assign(SerializedProperty array, TextMeshProUGUI[] labels)
    {
        array.arraySize = labels.Length;
        for (int i = 0; i < labels.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
    }

    /// <summary>A row label: full row width less the icon, pinned to the top or bottom of the row.</summary>
    private static void Anchor(RectTransform rect, bool top, float height)
    {
        rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rect.offsetMin = new Vector2(TextLeft, rect.offsetMin.y);
        rect.offsetMax = new Vector2(-24f, rect.offsetMax.y);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top ? -8f : 8f);
    }

    private static void FitOneLine(TextMeshProUGUI label, float max)
    {
        label.enableAutoSizing = true;
        label.fontSizeMin = UIHouseStyle.MinFontSize;
        label.fontSizeMax = max;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static RectTransform Button(string name, Transform parent, string text, string role, string labelRole,
        float size, out TextMeshProUGUI label)
    {
        var rect = RunOverlayUI.CreateChild(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(image, role);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        UIHouseStyle.ApplyButton(button, role);
        label = Label("Label", rect, text, size, RunOverlayUI.Parchment, labelRole, TextAlignmentOptions.Center);
        FitOneLine(label, size);
        RunOverlayUI.Stretch(label.rectTransform);
        return rect;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, string role,
        TextAlignmentOptions alignment)
    {
        var label = RunOverlayUI.CreateLabel(name, parent, text, size, color);
        UIHouseStyle.ApplyLabel(label, role);
        label.color = color;
        label.fontSize = size;
        label.alignment = alignment;
        return label;
    }
}
#endif
