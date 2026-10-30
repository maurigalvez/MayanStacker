#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the Try Again result card rows to the existing Game Over card in Game Scene UI.prefab
/// and wires them to <see cref="RunResultCard"/> and UIManager.
///
/// Reuses what is already there: the Final Score Label becomes the card's score and
/// NextGoalText moves inside the card as its goal line. Everything else is new and named, so
/// running it again finds the same objects instead of duplicating them.
///
/// Positions are a starting point for the 1080x1920 layout - restyle in the prefab afterwards.
/// Re-running puts the card's rows back at these defaults, so it asks first.
///
/// Menu: TamalStacker ▸ UI ▸ Set Up Try Again Card.
/// </summary>
public static class GameOverUISetup
{
    private const string PrefabPath = "Assets/Prefabs/Game Scene UI.prefab";
    private const string CardPath = "Background/Pause Box";
    private const string DialogTitle = "Try Again Card";

    private static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    private static readonly Color Parchment = new Color(0.93f, 0.90f, 0.82f, 1f);
    private static readonly Color Muted = new Color(0.72f, 0.68f, 0.58f, 1f);

    /// <summary>Row width inside the ~1320-wide card: 160 clear of each carved edge.</summary>
    private const float RowWidth = 1000f;

    /// <summary>
    /// The outer stat cells sit this far in from the slab's ends: the slab art's rounded rim
    /// takes about 8% of each side, and labels like "Best combo" otherwise run onto it.
    /// </summary>
    private const float StatSlabRim = 80f;

    /// <summary>Home and Try Again sit this far in from the card's left and right edges.</summary>
    private const float ButtonInset = 328f;

    /// <summary>Half the card's drawn height (1345 × 1.3 / 2), so the Daily labels sit just under it.</summary>
    private const float CardHalfHeight = 875f;

    [MenuItem("TamalStacker/UI/Set Up Try Again Card")]
    public static void SetUp()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            EditorUtility.DisplayDialog(DialogTitle, "Couldn't find " + PrefabPath + ".", "OK");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            string error = Build(root, out bool alreadySetUp, out bool proceed);
            if (!proceed) return;
            if (error != null)
            {
                EditorUtility.DisplayDialog(DialogTitle, error, "OK");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved)
            {
                Debug.LogError("[GameOverUISetup] Failed to save " + PrefabPath);
                return;
            }

            EditorUtility.DisplayDialog(DialogTitle,
                (alreadySetUp ? "Re-applied the default layout to " : "Added the result card to ") + PrefabPath + ".\n\n" +
                "The Game Over card now has a headline, flavour line, best line, three stats, the " +
                "jade row (emblem + large count-up, live as missions, the Path and the Offering pay) " +
                "and the goal line, all filled at runtime from localization. The Final Score Label is the " +
                "score and NextGoalText moved inside the card. Home and Try Again are mirrored, and the " +
                "Daily streak and countdown sit under the card at reading size.\n\n" +
                "Timing, colours and count-up sound are on the RunResultCard component on the Game " +
                "Over Panel. Restyle positions and fonts freely in the prefab.", "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        var asset = AssetDatabase.LoadAssetAtPath<Object>(PrefabPath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private static string Build(GameObject root, out bool alreadySetUp, out bool proceed)
    {
        alreadySetUp = false;
        proceed = true;

        var ui = root.GetComponentInChildren<UIManager>(true);
        if (ui == null) return "No UIManager in " + PrefabPath + ".";

        var uiSo = new SerializedObject(ui);
        var panel = uiSo.FindProperty("gameOverPanel").objectReferenceValue as GameObject;
        var score = uiSo.FindProperty("finalScoreText").objectReferenceValue as TextMeshProUGUI;
        var goal = uiSo.FindProperty("nextGoalText").objectReferenceValue as TextMeshProUGUI;
        if (panel == null || score == null || goal == null)
        {
            return "UIManager needs gameOverPanel, finalScoreText and nextGoalText assigned first.";
        }

        Transform cardTransform = panel.transform.Find(CardPath);
        if (cardTransform == null) return "Couldn't find '" + CardPath + "' under the Game Over Panel.";
        var card = (RectTransform)cardTransform;

        alreadySetUp = uiSo.FindProperty("runResultCard").objectReferenceValue != null;
        // Batch mode (-executeMethod GameOverUISetup.SetUp) can't answer the dialog, so it re-applies.
        if (alreadySetUp && !Application.isBatchMode && !EditorUtility.DisplayDialog(DialogTitle,
                "The result card is already set up.\n\nRe-apply the default layout? Positions and sizes " +
                "of its rows go back to defaults.", "Re-apply", "Cancel"))
        {
            proceed = false;
            return null;
        }

        // The jade row's emblem is the same art as the menu's jade balance, so the two read as one.
        var art = MainMenuRailSetup.LoadArt();
        if (art == null || art.jade == null)
        {
            return "The jade emblem isn't assigned. Run TamalStacker ▸ UI ▸ Main Menu Rail ▸ Build Rail " +
                   "(it creates the MenuRailArt asset) and assign its Jade sprite first.";
        }
        MainMenuRailSetup.PrepareJadeArt(art);

        TMP_FontAsset font = score.font != null ? score.font : RunOverlayUI.DisplayFont;

        // Card rows, top to bottom. Centre Y is measured down from the card's top edge. The rows
        // stack edge to edge between the card art's carved top border (65 in) and the top of
        // the buttons (~1040), so nothing crosses either. Sizes are large because the game canvas is
        // matched to a 1920-wide reference: on a portrait phone the card (×1.3) draws at about
        // 0.73 of these numbers, so a 54 reads like a 39.
        var headline = Label(card, "ResultHeadline", "So Close to Glory", 104, Gold, font);
        PlaceFromTop(headline.rectTransform, 130, RowWidth, 120);            // 70 - 190

        var flavour = Label(card, "ResultFlavour", "The gods were watching.", 54, Muted, font);
        PlaceFromTop(flavour.rectTransform, 224, RowWidth, 64);              // 192 - 256

        score.textWrappingMode = TextWrappingModes.NoWrap;
        score.enableAutoSizing = false;
        score.fontSize = 180;
        score.alignment = TextAlignmentOptions.Center;
        PlaceFromTop(score.rectTransform, 355, RowWidth, 190);               // 260 - 450

        var best = Label(card, "ResultBestLine", "Best 1240 · 60 to go", 56, Muted, font);
        PlaceFromTop(best.rectTransform, 485, RowWidth, 66);                 // 452 - 518

        var stats = FindOrCreate(card, "ResultStats");
        PlaceFromTop(stats, 613, RowWidth, 170);                             // 528 - 698
        var height = Stat(stats, "Height", 0, "23", "Height", font, out var heightLabel);
        var perfects = Stat(stats, "Perfects", 1, "9", "Perfects", font, out var perfectsLabel);
        var combo = Stat(stats, "Combo", 2, "x5", "Best combo", font, out var comboLabel);

        // Its own row, big and in bright jade, so what the run paid is never a footnote.
        var jadeRow = JadeRow(card, art.jade, font, 100f, 110f, out var jadeLabel);
        PlaceFromTop(jadeRow, 766, RowWidth, 120);                           // 706 - 826

        goal.transform.SetParent(card, false);
        goal.textWrappingMode = TextWrappingModes.Normal;
        goal.enableAutoSizing = true;
        goal.fontSizeMax = 52;
        goal.fontSizeMin = 42;
        goal.fontSize = 52;
        goal.alignment = TextAlignmentOptions.Center;
        // Holds the Serpent's Edge and Path receipts above the goal: up to three lines.
        PlaceFromTop(goal.rectTransform, 929, RowWidth, 190);                // 834 - 1024

        MirrorButtons(card);
        PlaceDailyLabels(uiSo);

        var cardComponent = panel.GetComponent<RunResultCard>();
        if (cardComponent == null) cardComponent = panel.AddComponent<RunResultCard>();

        var so = new SerializedObject(cardComponent);
        so.FindProperty("headlineText").objectReferenceValue = headline;
        so.FindProperty("flavourText").objectReferenceValue = flavour;
        so.FindProperty("scoreText").objectReferenceValue = score;
        so.FindProperty("bestLineText").objectReferenceValue = best;
        so.FindProperty("statsRow").objectReferenceValue = stats;
        so.FindProperty("heightValueText").objectReferenceValue = height;
        so.FindProperty("heightLabelText").objectReferenceValue = heightLabel;
        so.FindProperty("perfectsValueText").objectReferenceValue = perfects;
        so.FindProperty("perfectsLabelText").objectReferenceValue = perfectsLabel;
        so.FindProperty("comboValueText").objectReferenceValue = combo;
        so.FindProperty("comboLabelText").objectReferenceValue = comboLabel;
        so.FindProperty("goalText").objectReferenceValue = goal;
        so.FindProperty("jadeText").objectReferenceValue = jadeLabel;
        so.FindProperty("jadeRow").objectReferenceValue = jadeRow;
        so.FindProperty("jadeClink").objectReferenceValue = art.jadeClink;
        so.FindProperty("jadeChime").objectReferenceValue = art.jadeChime;
        so.ApplyModifiedPropertiesWithoutUndo();

        uiSo.FindProperty("runResultCard").objectReferenceValue = cardComponent;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        return null;
    }

    private static TextMeshProUGUI Stat(RectTransform row, string name, int column, string sampleValue,
        string sampleLabel, TMP_FontAsset font, out TextMeshProUGUI label)
    {
        var cell = FindOrCreate(row, name);
        cell.anchorMin = new Vector2(column / 3f, 0f);
        cell.anchorMax = new Vector2((column + 1) / 3f, 1f);
        cell.pivot = new Vector2(0.5f, 0.5f);
        cell.offsetMin = new Vector2(column == 0 ? StatSlabRim : 0f, 0f);
        cell.offsetMax = new Vector2(column == 2 ? -StatSlabRim : 0f, 0f);
        cell.localScale = Vector3.one;

        var value = Label(cell, "Value", sampleValue, 88, Parchment, font);
        value.rectTransform.anchorMin = new Vector2(0f, 0.4f);
        value.rectTransform.anchorMax = Vector2.one;
        value.rectTransform.offsetMin = Vector2.zero;
        value.rectTransform.offsetMax = Vector2.zero;

        label = Label(cell, "Label", sampleLabel, 54, Muted, font);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = new Vector2(1f, 0.4f);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        return value;
    }

    /// <summary>
    /// Home and Try Again mirror each other: same inset from each side of the card. They were
    /// placed by hand 337 and 319 in, so the pair sat visibly off-centre.
    /// </summary>
    private static void MirrorButtons(RectTransform card)
    {
        var home = card.Find("Home") as RectTransform;
        var retry = card.Find("Try Again Button") as RectTransform;
        if (home == null || retry == null)
        {
            Debug.LogWarning("[GameOverUISetup] Couldn't find 'Home' and 'Try Again Button' on the card; buttons left as they are.");
            return;
        }
        home.anchoredPosition = new Vector2(ButtonInset, home.anchoredPosition.y);
        retry.anchoredPosition = new Vector2(-ButtonInset, home.anchoredPosition.y);
    }

    /// <summary>
    /// The Daily streak and next-ritual countdown sit on the Game Over Panel, outside the card,
    /// and were made at 32 and 22: about 18 and 12 px on a phone. They move to just under the
    /// card (anchored to its centre, so every aspect from 9:21 to 3:4 keeps them on screen) at
    /// the same reading size as the card's own lines.
    /// </summary>
    private static void PlaceDailyLabels(SerializedObject uiSo)
    {
        // The panel isn't scaled like the card, so these draw at ~0.56: 72 reads like the card's 54.
        PlaceUnderCard(uiSo.FindProperty("dailyStreakText").objectReferenceValue as TextMeshProUGUI,
            CardHalfHeight + 70f, 90f, 72f);
        PlaceUnderCard(uiSo.FindProperty("dailyResetCountdownText").objectReferenceValue as TextMeshProUGUI,
            CardHalfHeight + 165f, 80f, 64f);
    }

    private static void PlaceUnderCard(TextMeshProUGUI label, float centreBelowMiddle, float height, float size)
    {
        if (label == null) return;
        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1500f, height);
        rt.anchoredPosition = new Vector2(0f, -centreBelowMiddle);
        rt.localScale = Vector3.one;

        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.enableAutoSizing = true;
        label.fontSizeMax = size;
        label.fontSize = size;
        label.fontSizeMin = Mathf.Round(size * 0.8f);
        UIHouseStyle.EnforceMinFontSize(label);
    }

    /// <summary>
    /// The jade emblem followed by "+N jade", centred together as one line whatever the amount's
    /// width. The label carries the house Title outline so it reads over the card. Shared with
    /// <see cref="LevelCompleteJadeSetup"/>, so both result surfaces show jade the same way.
    /// </summary>
    internal static RectTransform JadeRow(RectTransform card, Sprite emblem, TMP_FontAsset font,
        float fontSize, float emblemHeight, out TextMeshProUGUI label)
    {
        var row = FindOrCreate(card, "ResultJade");
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var iconRect = FindOrCreate(row, "Emblem");
        var icon = iconRect.GetComponent<Image>();
        if (icon == null) icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = emblem;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var iconLayout = iconRect.GetComponent<LayoutElement>();
        if (iconLayout == null) iconLayout = iconRect.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = Mathf.Round(emblemHeight * 1.53f);   // the emblem art is 1.53:1
        iconLayout.preferredHeight = emblemHeight;

        label = Label(row, "Amount", "+24 jade", fontSize, Parchment, font);
        UIHouseStyle.ApplyLabel(label, UIHouseStyle.Title);
        if (font != null) label.font = font;
        label.enableAutoSizing = false;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        UIHouseStyle.EnforceMinFontSize(label);
        var labelLayout = label.GetComponent<LayoutElement>();
        if (labelLayout == null) labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredHeight = Mathf.Round(fontSize * 1.15f);

        row.gameObject.SetActive(false);
        return row;
    }

    internal static TextMeshProUGUI Label(Transform parent, string name, string sample, float size,
        Color color, TMP_FontAsset font)
    {
        var rt = FindOrCreate(parent, name);
        var label = rt.GetComponent<TextMeshProUGUI>();
        if (label == null) label = rt.gameObject.AddComponent<TextMeshProUGUI>();

        // Font before the CJK switcher, which caches whatever font it wakes up to.
        if (font != null) label.font = font;
        label.text = sample;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        // Long translations shrink a little rather than clip, never below the house floor.
        label.enableAutoSizing = true;
        label.fontSizeMax = size;
        label.fontSizeMin = Mathf.Max(UIHouseStyle.MinFontSize, Mathf.Round(size * 0.7f));

        if (rt.GetComponent<LocaleFontSwitcher>() == null) rt.gameObject.AddComponent<LocaleFontSwitcher>();
        return label;
    }

    private static RectTransform FindOrCreate(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void PlaceFromTop(RectTransform rt, float centreFromTop, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, -centreFromTop);
        rt.localScale = Vector3.one;
    }
}
#endif
