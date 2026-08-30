#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/Resources/UI/LanguageSelectScreen.prefab — the editable version of the
/// first-launch language picker.
///
/// The picker used to exist only as code, so it was the one screen that couldn't be restyled
/// with the rest of the UI. This writes out a prefab that reproduces the code-built layout,
/// wired to <see cref="LanguageSelectView"/>; from there it's an ordinary prefab to open and
/// redress, and LanguageSelectScreen picks it up automatically at runtime.
///
/// Non-destructive: it refuses to overwrite an existing prefab unless you confirm, so
/// styling already done is never silently clobbered.
///
/// Menu: TamalStacker ▸ Localization ▸ Create Language Select Prefab
/// </summary>
public static class LanguageSelectPrefabSetup
{
    private const string PrefabPath = "Assets/Resources/UI/LanguageSelectScreen.prefab";

    // Same Mayan temple palette the code-built screen uses, so the generated prefab is a
    // faithful starting point rather than a different-looking placeholder.
    private static readonly Color Backdrop = new Color(0.06f, 0.05f, 0.04f, 0.97f);
    private static readonly Color Stone = new Color(0.13f, 0.16f, 0.15f, 1f);
    private static readonly Color StoneHighlight = new Color(0.09f, 0.42f, 0.34f, 1f);
    private static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    private static readonly Color Parchment = new Color(0.93f, 0.90f, 0.82f, 1f);
    private static readonly Color HintColor = new Color(0.72f, 0.68f, 0.58f, 1f);

    [MenuItem("TamalStacker/Localization/Create Language Select Prefab")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("Language Select Prefab",
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

        EditorUtility.DisplayDialog("Language Select Prefab",
            "Created " + PrefabPath + ".\n\n" +
            "Open it to restyle the picker. The language rows are cloned from the " +
            "OptionTemplate button at runtime, so styling that one button styles them all.\n\n" +
            "Delete the prefab to go back to the code-built layout.", "OK");
    }

    private static GameObject BuildHierarchy()
    {
        var root = new GameObject("LanguageSelectScreen",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LanguageSelectView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // Full-bleed backdrop, which also eats taps so nothing behind can be hit.
        var backdrop = CreateChild("Backdrop", root.transform);
        Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = Backdrop;

        var title = CreateLabel("Title", root.transform, "Language", 64, Gold);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(900f, 90f));

        var subtitle = CreateLabel("Subtitle", root.transform, "Idioma / 语言 / 日本語", 34, Parchment);
        Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(900f, 60f));

        // A layout group positions the rows here, instead of the hand-computed offsets the
        // code path uses — one less thing to re-derive when the rows are restyled.
        var options = CreateChild("Options", root.transform);
        Place(options, new Vector2(0.5f, 1f), new Vector2(0f, -876f), new Vector2(760f, 890f));
        var layout = options.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 22f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var template = CreateChild("OptionTemplate", options);
        template.sizeDelta = new Vector2(760f, 130f);
        var templateImage = template.gameObject.AddComponent<Image>();
        templateImage.color = Stone;

        var templateLabel = CreateLabel("Label", template, "Language", 44, Parchment);
        Stretch(templateLabel.rectTransform);

        var button = template.gameObject.AddComponent<Button>();
        button.targetGraphic = templateImage;

        var hint = CreateLabel("FirstLaunchHint", root.transform,
            "You can change this later in Settings.", 26, HintColor);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(900f, 50f));

        var so = new SerializedObject(root.GetComponent<LanguageSelectView>());
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subtitleText").objectReferenceValue = subtitle;
        so.FindProperty("firstLaunchHint").objectReferenceValue = hint.gameObject;
        so.FindProperty("optionsContainer").objectReferenceValue = options;
        so.FindProperty("optionTemplate").objectReferenceValue = button;
        so.FindProperty("currentLocaleColor").colorValue = StoneHighlight;
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

        // The game's own font rather than TMP's LiberationSans default. The picker sets
        // per-locale fonts at runtime on top of this, so no LocaleFontSwitcher here.
        if (RunOverlayUI.DisplayFont != null) label.font = RunOverlayUI.DisplayFont;

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
