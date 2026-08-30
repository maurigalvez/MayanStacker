#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/Resources/UI/FtueTutorial.prefab — the editable face of the first-run
/// tutorial.
///
/// The tutorial used to speak through UIManager's instruction label and build its Skip
/// control in code, so the first screen a new player reads was the one screen that couldn't
/// be restyled or given the intended font. This writes out a prefab that reproduces the
/// code-built presentation, wired to <see cref="FtueTutorialView"/>; from there it's an
/// ordinary prefab to open and redress, and FtueTutorial picks it up automatically at
/// runtime.
///
/// Non-destructive: it refuses to overwrite an existing prefab unless you confirm, so
/// styling already done is never silently clobbered.
///
/// Menu: TamalStacker ▸ FTUE ▸ Create Tutorial Prefab
/// </summary>
public static class FtueTutorialPrefabSetup
{
    private const string PrefabPath = "Assets/Resources/UI/FtueTutorial.prefab";

    // Same Mayan temple palette as the code-built controls, so the generated prefab is a
    // faithful starting point rather than a different-looking placeholder.
    private static readonly Color Stone = new Color(0.06f, 0.09f, 0.08f, 0.55f);
    private static readonly Color Parchment = new Color(0.93f, 0.90f, 0.82f, 1f);
    private static readonly Color SkipLabel = new Color(0.85f, 0.89f, 0.85f, 0.9f);

    [MenuItem("TamalStacker/FTUE/Create Tutorial Prefab")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("FTUE Tutorial Prefab",
                PrefabPath + " already exists.\n\nReplace it? Any styling done to it will be lost.",
                "Replace", "Cancel"))
        {
            return;
        }

        EnsureResourcesSubfolder("UI");

        GameObject root = BuildHierarchy();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        EditorUtility.DisplayDialog("FTUE Tutorial Prefab",
            "Created " + PrefabPath + ".\n\n" +
            "Open it to set the intended font on MessageText and SkipButton/Label, and to " +
            "restyle the banner. The copy shown is only a preview — at runtime it comes from " +
            "the localization table.\n\n" +
            "Delete the prefab to go back to the code-built presentation.", "OK");
    }

    private static GameObject BuildHierarchy()
    {
        var root = new GameObject("FtueTutorial",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(FtueTutorialView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the game UI, below the language picker (5000), which must never be covered.
        canvas.sortingOrder = 4900;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // The banner sits below the middle of the screen: the block being taught about is
        // swinging across the top, and a banner over it would hide the thing it describes.
        var messagePanel = CreateChild("MessagePanel", root.transform);
        Place(messagePanel, new Vector2(0.5f, 0.5f), new Vector2(0f, -180f), new Vector2(920f, 160f));
        var panelImage = messagePanel.gameObject.AddComponent<Image>();
        panelImage.color = Stone;
        panelImage.raycastTarget = false; // never swallow the tap that drops the block

        var message = CreateLabel("MessageText", messagePanel, "Tap to drop.", 44, Parchment);
        Stretch(message.rectTransform);
        message.enableWordWrapping = true;

        // A quiet corner control: it should sit back rather than compete with the copy.
        var skip = CreateChild("SkipButton", root.transform);
        skip.anchorMin = skip.anchorMax = skip.pivot = new Vector2(1f, 0f);
        skip.anchoredPosition = new Vector2(-28f, 28f);
        skip.sizeDelta = new Vector2(150f, 62f);

        var skipImage = skip.gameObject.AddComponent<Image>();
        skipImage.color = Stone;

        var skipButton = skip.gameObject.AddComponent<Button>();
        skipButton.targetGraphic = skipImage;

        var skipText = CreateLabel("Label", skip, "Skip", 30, SkipLabel);
        Stretch(skipText.rectTransform);

        var so = new SerializedObject(root.GetComponent<FtueTutorialView>());
        so.FindProperty("messageText").objectReferenceValue = message;
        so.FindProperty("messagePanel").objectReferenceValue = messagePanel.gameObject;
        so.FindProperty("skipButton").objectReferenceValue = skipButton;
        so.FindProperty("skipLabel").objectReferenceValue = skipText;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static void EnsureResourcesSubfolder(string child)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources/" + child))
        {
            AssetDatabase.CreateFolder("Assets/Resources", child);
        }
    }

    private static RectTransform CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, float size, Color color)
    {
        var rt = CreateChild(name, parent);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = false;

        // The game's own font, not TMP's LiberationSans default — a generated prefab should
        // look like this game on the first run, before anyone restyles it.
        if (RunOverlayUI.DisplayFont != null) label.font = RunOverlayUI.DisplayFont;
        else if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;

        // Keeps the tutorial readable in Chinese and Japanese, the same way every other
        // localized label does.
        rt.gameObject.AddComponent<LocaleFontSwitcher>();

        return label;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        rt.localScale = Vector3.one;
    }
}
#endif
