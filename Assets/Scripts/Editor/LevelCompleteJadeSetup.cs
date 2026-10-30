#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds the jade row to the level-complete panel in Game Scene UI.prefab and wires it to
/// <see cref="LevelCompleteJade"/> and UIManager, so a temple win shows what it paid (the run's
/// jade plus stars won for the first time) the way the Try Again card does.
///
/// The row is built by <see cref="GameOverUISetup.JadeRow"/>, the same emblem and "+N jade"
/// line as the Try Again card. To make room, the score moves up into the space under the stars;
/// the jade row takes the space between it and the Next Level button.
///
/// Positions are measured down from the top of the panel's Pause Box (1750 × 2613). The panel
/// isn't scaled, so on a portrait phone it draws at about 0.56 of these numbers: the jade line's
/// 150 reads like 84, above the 56 the jade rule asks of result surfaces.
///
/// Menu: TamalStacker ▸ UI ▸ Set Up Level Complete Jade.
/// </summary>
public static class LevelCompleteJadeSetup
{
    private const string PrefabPath = "Assets/Prefabs/Game Scene UI.prefab";
    private const string BoxPath = "Background/Pause Box";
    private const string DialogTitle = "Level Complete Jade";

    private const float RowWidth = 1200f;

    private static readonly Color PathJade = new Color(0.09f, 0.57f, 0.44f, 1f);

    [MenuItem("TamalStacker/UI/Set Up Level Complete Jade")]
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
            string error = Build(root);
            if (error != null)
            {
                EditorUtility.DisplayDialog(DialogTitle, error, "OK");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved)
            {
                Debug.LogError("[LevelCompleteJadeSetup] Failed to save " + PrefabPath);
                return;
            }

            EditorUtility.DisplayDialog(DialogTitle,
                "Added the jade row to the level-complete panel in " + PrefabPath + ".\n\n" +
                "After the score counts up, the Path line (\"Path +46 XP\") fades in and the row counts " +
                "up what the win paid (the run, new stars, missions and the Path) with a clink, chime, " +
                "punch and haptic. The score moved up under the stars to make room. Timing and colour " +
                "are on the LevelCompleteJade component.", "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        var asset = AssetDatabase.LoadAssetAtPath<Object>(PrefabPath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private static string Build(GameObject root)
    {
        var ui = root.GetComponentInChildren<UIManager>(true);
        if (ui == null) return "No UIManager in " + PrefabPath + ".";

        var uiSo = new SerializedObject(ui);
        var panel = uiSo.FindProperty("levelCompletePanel").objectReferenceValue as GameObject;
        var score = uiSo.FindProperty("levelScoreText").objectReferenceValue as TextMeshProUGUI;
        if (panel == null || score == null)
        {
            return "UIManager needs levelCompletePanel and levelScoreText assigned first.";
        }

        var box = panel.transform.Find(BoxPath) as RectTransform;
        if (box == null) return "Couldn't find '" + BoxPath + "' under the level-complete panel.";

        var art = MainMenuRailSetup.LoadArt();
        if (art == null || art.jade == null)
        {
            return "The jade emblem isn't assigned. Run TamalStacker ▸ UI ▸ Main Menu Rail ▸ Build Rail " +
                   "(it creates the MenuRailArt asset) and assign its Jade sprite first.";
        }
        MainMenuRailSetup.PrepareJadeArt(art);

        // Stars end around 1372 and Next Level starts at 1858; score, Path line and jade stack
        // edge to edge between them.
        var scoreRect = score.rectTransform;
        scoreRect.anchorMin = new Vector2(0f, 1f);
        scoreRect.anchorMax = new Vector2(1f, 1f);
        scoreRect.pivot = new Vector2(0.5f, 0.5f);
        scoreRect.sizeDelta = new Vector2(-406f, 190f);
        scoreRect.anchoredPosition = new Vector2(0f, -1475f);                // 1380 - 1570
        UIHouseStyle.EnforceMinFontSize(score);

        TMP_FontAsset font = score.font != null ? score.font : RunOverlayUI.DisplayFont;

        // "Path +46 XP": 80 reads like 45 on a phone. Filled and shown by LevelCompleteJade.
        var path = GameOverUISetup.Label(box, "ResultPath", "Path +46 XP", 80f, PathJade, font);
        var pathRect = path.rectTransform;
        pathRect.anchorMin = pathRect.anchorMax = new Vector2(0.5f, 1f);
        pathRect.pivot = new Vector2(0.5f, 0.5f);
        pathRect.sizeDelta = new Vector2(RowWidth, 70f);
        pathRect.anchoredPosition = new Vector2(0f, -1610f);                 // 1575 - 1645
        pathRect.localScale = Vector3.one;
        UIHouseStyle.EnforceMinFontSize(path);
        path.gameObject.SetActive(false);

        var row = GameOverUISetup.JadeRow(box, art.jade, font, 150f, 150f, out var amount);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(RowWidth, 190f);
        row.anchoredPosition = new Vector2(0f, -1748f);                      // 1653 - 1843
        row.localScale = Vector3.one;

        // Kept active and hidden by alpha, so it hears jade that lands while the panel is up.
        row.gameObject.SetActive(true);
        var group = row.GetComponent<CanvasGroup>();
        if (group == null) group = row.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        var jade = row.GetComponent<LevelCompleteJade>();
        if (jade == null) jade = row.gameObject.AddComponent<LevelCompleteJade>();
        var so = new SerializedObject(jade);
        so.FindProperty("amountText").objectReferenceValue = amount;
        so.FindProperty("pathText").objectReferenceValue = path;
        so.FindProperty("jadeClink").objectReferenceValue = art.jadeClink;
        so.FindProperty("jadeChime").objectReferenceValue = art.jadeChime;
        so.ApplyModifiedPropertiesWithoutUndo();

        uiSo.FindProperty("levelCompleteJade").objectReferenceValue = jade;
        uiSo.ApplyModifiedPropertiesWithoutUndo();
        return null;
    }
}
#endif
