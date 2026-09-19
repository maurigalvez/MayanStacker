#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/Resources/UI/AppUpdatePrompt.prefab — the editable face of the Google
/// Play in-app update prompt.
///
/// The prompt used to exist only as code, so it was the one main menu surface that couldn't
/// be restyled with the rest of the UI. This writes out a prefab that reproduces the
/// code-built layout, wired to <see cref="AppUpdatePromptView"/>; from there it's an ordinary
/// prefab to open and redress, and AppUpdatePrompt picks it up automatically at runtime.
///
/// Non-destructive: it refuses to overwrite an existing prefab unless you confirm, so
/// styling already done is never silently clobbered.
///
/// Menu: TamalStacker ▸ Retention ▸ Create App Update Prompt Prefab
/// </summary>
public static class AppUpdatePromptPrefabSetup
{
    private const string PrefabPath = "Assets/Resources/UI/AppUpdatePrompt.prefab";

    [MenuItem("TamalStacker/Retention/Create App Update Prompt Prefab")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("App Update Prompt Prefab",
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

        EditorUtility.DisplayDialog("App Update Prompt Prefab",
            "Created " + PrefabPath + ".\n\n" +
            "Open it to restyle the modal and the download strip. Everything is left visible " +
            "for editing — the view hides it all at runtime. The copy shown is only a preview; " +
            "the real text comes from the localization table.\n\n" +
            "Progress fill: either keep Fill left-anchored, or set its Image to Filled " +
            "(Horizontal) and the bar will drive fillAmount instead.\n\n" +
            "Delete the prefab to go back to the code-built layout.", "OK");
    }

    private static GameObject BuildHierarchy()
    {
        var root = new GameObject("AppUpdatePrompt",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AppUpdatePromptView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = AppUpdatePromptView.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // ── Backdrop ── the only full-screen raycast target; it blocks the menu only while
        // a decision is up.
        var backdropRect = CreateChild("Backdrop", root.transform);
        Stretch(backdropRect);
        var backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = RunOverlayUI.Backdrop;
        backdrop.raycastTarget = true;

        // ── Modal ──
        var modal = CreateChild("Modal", root.transform);
        Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));
        modal.gameObject.AddComponent<Image>().color = RunOverlayUI.Stone;

        var title = CreateLabel("Title", modal, "A New Offering Awaits", 52f, RunOverlayUI.Gold);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(800f, 120f));

        var body = CreateLabel("Body", modal,
            "A newer carving of the temple is ready. Take it now — you can keep stacking while it arrives.", 32f, RunOverlayUI.Parchment);
        Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(800f, 220f));

        var primary = CreateButton("Primary", modal, "Update", RunOverlayUI.Jade, out var primaryLabel);
        Place((RectTransform)primary.transform, new Vector2(0.5f, 0f), new Vector2(220f, 100f), new Vector2(400f, 110f));

        var secondary = CreateButton("Secondary", modal, "Not now", RunOverlayUI.Clay, out var secondaryLabel);
        Place((RectTransform)secondary.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 100f), new Vector2(400f, 110f));

        // ── Download strip ── never blocks input; the player keeps playing while it runs.
        var strip = CreateChild("DownloadStrip", root.transform);
        Place(strip, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 120f));
        var stripBg = strip.gameObject.AddComponent<Image>();
        stripBg.color = RunOverlayUI.Stone;
        stripBg.raycastTarget = false;

        var stripLabel = CreateLabel("Label", strip, "Carrying the new stones… 40%", 28f, RunOverlayUI.Parchment);
        Place(stripLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(840f, 48f));

        var track = CreateChild("Track", strip);
        Place(track, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(840f, 16f));
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0f, 0f, 0f, 0.45f);
        trackImage.raycastTarget = false;

        // Left-anchored, previewed at 40% so the bar is visible while restyling.
        var fill = CreateChild("Fill", track);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0.4f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = RunOverlayUI.Gold;
        fillImage.raycastTarget = false;

        var so = new SerializedObject(root.GetComponent<AppUpdatePromptView>());
        so.FindProperty("backdrop").objectReferenceValue = backdrop;
        so.FindProperty("modal").objectReferenceValue = modal;
        so.FindProperty("modalTitle").objectReferenceValue = title;
        so.FindProperty("modalBody").objectReferenceValue = body;
        so.FindProperty("primaryButton").objectReferenceValue = primary;
        so.FindProperty("primaryLabel").objectReferenceValue = primaryLabel;
        so.FindProperty("secondaryButton").objectReferenceValue = secondary;
        so.FindProperty("secondaryLabel").objectReferenceValue = secondaryLabel;
        so.FindProperty("strip").objectReferenceValue = strip;
        so.FindProperty("stripLabel").objectReferenceValue = stripLabel;
        so.FindProperty("progressFill").objectReferenceValue = fill;
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
        label.enableWordWrapping = true;

        // The game's own font, then the CJK switcher — in that order, because the switcher
        // caches whatever font it finds as the label's "original" when it wakes up.
        if (RunOverlayUI.DisplayFont != null) label.font = RunOverlayUI.DisplayFont;
        else if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        rt.gameObject.AddComponent<LocaleFontSwitcher>();

        return label;
    }

    private static Button CreateButton(string name, Transform parent, string text, Color background,
        out TextMeshProUGUI label)
    {
        var rt = CreateChild(name, parent);

        var image = rt.gameObject.AddComponent<Image>();
        image.color = background;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        label = CreateLabel("Label", rt, text, 34f, RunOverlayUI.Parchment);
        Stretch(label.rectTransform);

        return button;
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
