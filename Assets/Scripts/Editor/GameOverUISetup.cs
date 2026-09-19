#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

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
                "The Game Over card now has a headline, flavour line, best line, three stats and the " +
                "goal line, all filled at runtime from localization. The Final Score Label is the " +
                "score and NextGoalText moved inside the card.\n\n" +
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
        if (alreadySetUp && !EditorUtility.DisplayDialog(DialogTitle,
                "The result card is already set up.\n\nRe-apply the default layout? Positions and sizes " +
                "of its rows go back to defaults.", "Re-apply", "Cancel"))
        {
            proceed = false;
            return null;
        }

        TMP_FontAsset font = score.font != null ? score.font : RunOverlayUI.DisplayFont;

        // Card rows, top to bottom. Centre Y is measured down from the card's top edge; the
        // buttons start around 766, so everything stays above that.
        var headline = Label(card, "ResultHeadline", "So Close to Glory", 84, Gold, font);
        PlaceFromTop(headline.rectTransform, 95, 820, 100);

        var flavour = Label(card, "ResultFlavour", "The gods were watching.", 40, Muted, font);
        PlaceFromTop(flavour.rectTransform, 170, 820, 60);

        score.textWrappingMode = TextWrappingModes.NoWrap;
        score.enableAutoSizing = false;
        score.fontSize = 170;
        score.alignment = TextAlignmentOptions.Center;
        PlaceFromTop(score.rectTransform, 300, 820, 190);

        var best = Label(card, "ResultBestLine", "Best 1240 · 60 to go", 44, Muted, font);
        PlaceFromTop(best.rectTransform, 425, 820, 60);

        var stats = FindOrCreate(card, "ResultStats");
        PlaceFromTop(stats, 540, 820, 140);
        var height = Stat(stats, "Height", 0, "23", "Height", font, out var heightLabel);
        var perfects = Stat(stats, "Perfects", 1, "9", "Perfects", font, out var perfectsLabel);
        var combo = Stat(stats, "Combo", 2, "x5", "Best combo", font, out var comboLabel);

        goal.transform.SetParent(card, false);
        goal.textWrappingMode = TextWrappingModes.Normal;
        goal.enableAutoSizing = false;
        goal.fontSize = 40;
        goal.alignment = TextAlignmentOptions.Center;
        PlaceFromTop(goal.rectTransform, 675, 820, 90);

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
        cell.offsetMin = Vector2.zero;
        cell.offsetMax = Vector2.zero;
        cell.localScale = Vector3.one;

        var value = Label(cell, "Value", sampleValue, 64, Parchment, font);
        value.rectTransform.anchorMin = new Vector2(0f, 0.4f);
        value.rectTransform.anchorMax = Vector2.one;
        value.rectTransform.offsetMin = Vector2.zero;
        value.rectTransform.offsetMax = Vector2.zero;

        label = Label(cell, "Label", sampleLabel, 32, Muted, font);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = new Vector2(1f, 0.4f);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        return value;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string sample, float size,
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
