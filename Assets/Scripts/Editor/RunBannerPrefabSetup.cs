#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/Resources/UI/RunBanner.prefab — the editable version of the mid-run
/// banner that announces altitude bands, temple objectives and first-time block intros.
///
/// The banner was built in code, so it had no design-time font and rendered in TMP's
/// LiberationSans default — a plain sans in the middle of a game that isn't. This writes out
/// a prefab reproducing the code-built layout, wired to <see cref="RunBannerView"/>; from
/// there it's an ordinary prefab to open and redress, and RunBanner picks it up
/// automatically at runtime.
///
/// Non-destructive: it refuses to overwrite an existing prefab unless you confirm, so
/// styling already done is never silently clobbered.
///
/// Menu: TamalStacker ▸ UI ▸ Create Run Banner Prefab
/// </summary>
public static class RunBannerPrefabSetup
{
    private const string PrefabPath = "Assets/Resources/UI/RunBanner.prefab";

    [MenuItem("TamalStacker/UI/Create Run Banner Prefab")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("Run Banner Prefab",
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

        EditorUtility.DisplayDialog("Run Banner Prefab",
            "Created " + PrefabPath + ".\n\n" +
            "Open it to style the banner — a frame behind the text is fine, the CanvasGroup " +
            "fades the whole thing. The copy shown is only a preview; at runtime it comes " +
            "from whatever is being announced.\n\n" +
            "Callers place the banner vertically (block intros sit lower than altitude " +
            "banners); turn off Follow Caller Placement to pin it where you author it.\n\n" +
            "Delete the prefab to go back to the code-built banner.", "OK");
    }

    private static GameObject BuildHierarchy()
    {
        var root = new GameObject("RunBanner",
            typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(RunBannerView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the HUD, below anything that takes input. No GraphicRaycaster at all: a
        // banner that ate taps would swallow the drop and make the game feel broken.
        canvas.sortingOrder = 400;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        var group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // Title and subtitle move as one unit, so callers place the pair rather than the
        // headline alone — and so a frame added later travels with them.
        var content = CreateChild("Content", root.transform);
        Place(content, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(920f, 300f));

        var title = CreateLabel("Title", content, "Jade Sliver", 60f, RunOverlayUI.Gold);
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 160f));

        // The 95px drop matches the code-built banner, so the generated prefab reads the
        // same as what it replaces.
        var subtitle = CreateLabel("Subtitle", content,
            "Narrow, but scores triple. Land it if your hand is steady.", 32f, RunOverlayUI.Parchment);
        Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -95f), new Vector2(860f, 120f));

        var so = new SerializedObject(root.GetComponent<RunBannerView>());
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subtitleText").objectReferenceValue = subtitle;
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("canvasGroup").objectReferenceValue = group;
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

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, string text,
        float size, Color color)
    {
        var rt = CreateChild(name, parent);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = true;

        // The game's font rather than TMP's default, which is the whole reason this prefab
        // exists; then the CJK switcher, which caches whatever font it wakes up to.
        if (RunOverlayUI.DisplayFont != null) label.font = RunOverlayUI.DisplayFont;
        else if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;

        rt.gameObject.AddComponent<LocaleFontSwitcher>();

        return label;
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
