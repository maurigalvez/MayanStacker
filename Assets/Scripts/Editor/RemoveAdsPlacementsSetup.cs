#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the two Remove Ads placements outside Settings, both wearing the No Ads icon
/// (Assets/Art/UI/T_MayanStacker_NoAds_Icon.png, imported as a sprite here if needed):
///
///   • TamalStacker ▸ UI ▸ Create Remove Ads Nudge Prefab — generates
///     Assets/Resources/UI/RemoveAdsNudge.prefab, the post-ad strip on the result card
///     (<see cref="RemoveAdsNudgeView"/>): stone slab, icon on the left, one line of text.
///   • TamalStacker ▸ Monetization ▸ Add Remove Ads Menu Chip — adds the "No Ads" chip
///     (<see cref="RemoveAdsMenuChip"/>) to the Main Menu panel inside the Main Menu Canvas
///     prefab, top-right. It edits the prefab asset, not the scene, so it works with the
///     MainMenu scene open or closed.
///
/// Dressed from the house style (<see cref="UIHouseStyle"/>). Both are non-destructive: the
/// nudge prefab asks before replacing, and the chip is skipped if one already exists.
/// Positions are a starting point — move them if they overlap anything.
/// </summary>
public static class RemoveAdsPlacementsSetup
{
    private const string IconPath = "Assets/Art/UI/T_MayanStacker_NoAds_Icon.png";
    private const string NudgePrefabPath = "Assets/Resources/UI/RemoveAdsNudge.prefab";
    private const string MainMenuPrefabPath = "Assets/Prefabs/Main Menu/Main Menu Canvas.prefab";
    private const string ChipName = "RemoveAdsChip";

    // Chip layout, from the top-right corner of the Main Menu panel.
    private static readonly Vector2 ChipPosition = new Vector2(-30f, -130f);
    private static readonly Vector2 ChipSize = new Vector2(180f, 210f);
    private const float ChipIconSize = 150f;
    private const float ChipLabelHeight = 50f;

    #region Nudge prefab

    [MenuItem("TamalStacker/UI/Create Remove Ads Nudge Prefab")]
    public static void CreateNudgePrefab()
    {
        const string title = "Remove Ads Nudge Prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(NudgePrefabPath) != null &&
            !EditorUtility.DisplayDialog(title,
                NudgePrefabPath + " already exists.\n\nReplace it? Any styling done to it will be lost.",
                "Replace", "Cancel"))
        {
            return;
        }

        Sprite icon = EnsureIconSprite();

        GameObject root = BuildNudge(icon);
        PrefabUtility.SaveAsPrefabAsset(root, NudgePrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(NudgePrefabPath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        EditorUtility.DisplayDialog(title,
            "Created " + NudgePrefabPath + ".\n\n" + StyleNote() + (icon == null ? "\n\n" + MissingIconNote() : "") +
            "\n\nThe strip is left visible for editing — the view hides it at runtime and fills in the " +
            "localized text. Delete the prefab to go back to the code-built strip.", "OK");
    }

    private static GameObject BuildNudge(Sprite icon)
    {
        var root = new GameObject("RemoveAdsNudge",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RemoveAdsNudgeView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = RemoveAdsNudgeView.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // ── Strip ── the stone slab from the reference, tappable as a whole.
        var nudge = RunOverlayUI.CreateChild("Nudge", root.transform);
        var slab = nudge.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;
        slab.raycastTarget = true;
        UIHouseStyle.ApplyImage(slab, UIHouseStyle.Modal);
        RunOverlayUI.Place(nudge, RemoveAdsNudgeView.NudgeAnchor, RemoveAdsNudgeView.NudgePosition, RemoveAdsNudgeView.NudgeSize);

        var button = nudge.gameObject.AddComponent<Button>();
        button.targetGraphic = slab;

        // ── Icon ── left edge, vertically centred.
        float iconSpace = 0f;
        if (icon != null)
        {
            var iconRect = RunOverlayUI.CreateChild("Icon", nudge);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(RemoveAdsNudgeView.IconInset, 0f);
            iconRect.sizeDelta = RemoveAdsNudgeView.IconSize;
            var image = iconRect.gameObject.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            iconSpace = RemoveAdsNudgeView.IconInset + RemoveAdsNudgeView.IconSize.x;
        }

        // ── Label ── the rest of the strip, in the reference's body look.
        var label = RunOverlayUI.CreateLabel("Label", nudge, "Tired of ads? Remove them", 36f, RunOverlayUI.Parchment);
        UIHouseStyle.ApplyLabel(label, UIHouseStyle.Body);
        label.enableAutoSizing = true;
        label.fontSizeMin = 24f;
        label.fontSizeMax = 38f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(iconSpace + 16f, 10f);
        labelRect.offsetMax = new Vector2(-24f, -10f);

        var so = new SerializedObject(root.GetComponent<RemoveAdsNudgeView>());
        so.FindProperty("nudge").objectReferenceValue = nudge;
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    #endregion

    #region Main menu chip

    [MenuItem("TamalStacker/Monetization/Add Remove Ads Menu Chip")]
    public static void AddMenuChip()
    {
        const string title = "Remove Ads Menu Chip";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath) == null)
        {
            EditorUtility.DisplayDialog(title, MainMenuPrefabPath + " was not found.", "OK");
            return;
        }

        Sprite icon = EnsureIconSprite();
        if (icon == null)
        {
            EditorUtility.DisplayDialog(title, MissingIconNote(), "OK");
            return;
        }

        string result;
        GameObject root = PrefabUtility.LoadPrefabContents(MainMenuPrefabPath);
        try
        {
            result = BuildChip(root, icon);
            if (result == null) PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (result != null)
        {
            EditorUtility.DisplayDialog(title, result, "OK");
            return;
        }

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath);
        EditorGUIUtility.PingObject(asset);
        EditorUtility.DisplayDialog(title,
            "Added the No Ads chip to the Main Menu panel in " + MainMenuPrefabPath + " (top-right, under the " +
            "player strip).\n\nIt stays hidden in play until the FTUE ad grace period is over and Google Play has " +
            "returned a price, and disappears once Remove Ads is owned. Open the prefab to move it if it overlaps " +
            "anything.", "OK");
    }

    /// <returns>Null on success, or the reason nothing was built.</returns>
    private static string BuildChip(GameObject root, Sprite icon)
    {
        var menu = root.GetComponentInChildren<MainMenuManager>(true);
        var panel = menu != null
            ? new SerializedObject(menu).FindProperty("mainMenuPanel").objectReferenceValue as GameObject
            : null;
        if (panel == null)
            return "The prefab has no Main Menu panel (MainMenuManager.mainMenuPanel is unassigned).";

        if (panel.transform.Find(ChipName) != null)
            return "The Main Menu panel already has a " + ChipName + " — nothing to do.\n\nDelete it first to rebuild it.";

        var chip = RunOverlayUI.CreateChild(ChipName, panel.transform);
        chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f);
        chip.pivot = new Vector2(1f, 1f);
        chip.anchoredPosition = ChipPosition;
        chip.sizeDelta = ChipSize;
        chip.SetAsLastSibling();

        // ── Icon ── doubles as the button's target graphic.
        var iconRect = RunOverlayUI.CreateChild("Icon", chip);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(ChipIconSize, ChipIconSize);
        var image = iconRect.gameObject.AddComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = true;

        // ── Label ── "No Ads", localized by LocalizedText.
        var label = RunOverlayUI.CreateLabel("Label", chip, "No Ads", 30f, RunOverlayUI.Gold);
        UIHouseStyle.ApplyLabel(label, UIHouseStyle.Title);
        label.enableAutoSizing = true;
        label.fontSizeMin = 20f;
        label.fontSizeMax = 32f;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(0f, ChipLabelHeight);

        var localized = label.gameObject.AddComponent<LocalizedText>();
        var localizedSo = new SerializedObject(localized);
        var keyProp = localizedSo.FindProperty("localizationKey");
        if (keyProp != null)
        {
            keyProp.stringValue = "remove_ads_chip";
            localizedSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        chip.gameObject.AddComponent<CanvasGroup>();
        chip.gameObject.AddComponent<RemoveAdsMenuChip>();

        return null;
    }

    #endregion

    /// <summary>Imports the icon as a single UI sprite (no mips, alpha kept) and returns it.</summary>
    private static Sprite EnsureIconSprite()
    {
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer == null) return null;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.maxTextureSize != 256) { importer.maxTextureSize = 256; changed = true; }

        if (changed) importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    private static string StyleNote() => UIHouseStyle.HasReference
        ? "Styled from " + UIHouseStyle.ReferencePrefabPath + "."
        : UIHouseStyle.ReferencePrefabPath + " was not found, so the MainMenu_UI sheet defaults were used.";

    private static string MissingIconNote() =>
        IconPath + " was not found, so there is no icon. Put the No Ads icon there and run this again.";
}
#endif
