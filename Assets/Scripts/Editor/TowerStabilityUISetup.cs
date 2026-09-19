#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the UI the tremor meter needs, without touching any existing UI.
///
/// Writes only two assets, both under Assets/Resources:
///  - UI/TowerStabilityMeter.prefab - the meter, wired to <see cref="TowerStabilityView"/>,
///    reproducing the code-built bar so switching to it isn't also a change of layout.
///  - StabilitySettings.asset - the tuning, seeded with the code defaults.
///
/// No scene, no Game Scene UI prefab and no UIManager field is modified: the meter lives on
/// its own non-interactive overlay canvas, and TowerStability picks both assets up at runtime.
/// Delete either to fall back to the code-built bar / code defaults.
///
/// Non-destructive: an existing prefab is only replaced after confirmation, and an existing
/// settings asset is never overwritten.
///
/// Menu: TamalStacker ▸ UI ▸ Set Up Tremor Meter UI (and the pieces individually).
/// </summary>
public static class TowerStabilityUISetup
{
    private const string PrefabPath = "Assets/Resources/" + TowerStability.MeterPrefabResourcePath + ".prefab";
    private const string SettingsPath = "Assets/Resources/" + StabilitySettings.ResourcePath + ".asset";
    private const string EdgePrefabPath = "Assets/Resources/" + RiskWindow.ViewPrefabResourcePath + ".prefab";
    private const string DialogTitle = "Tremor Meter UI";

    [MenuItem("TamalStacker/UI/Set Up Tremor Meter UI")]
    public static void SetUpAll()
    {
        bool settingsCreated = EnsureSettingsAsset();
        bool prefabCreated = CreatePrefabInternal(askBeforeReplace: true);
        bool edgePrefabCreated = CreateEdgePrefabInternal(askBeforeReplace: true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(DialogTitle,
            (settingsCreated ? "Created " : "Kept existing ") + SettingsPath + "\n" +
            (prefabCreated ? "Created " : "Kept existing ") + PrefabPath + "\n" +
            (edgePrefabCreated ? "Created " : "Kept existing ") + EdgePrefabPath + "\n\n" +
            "An existing settings asset keeps its values, and new Serpent's Edge fields take " +
            "their code defaults. An older meter prefab still shows the Edge cost preview - " +
            "it's built at runtime if the prefab has no EdgePreview.\n\n" +
            "No existing scene or UI prefab was modified - the meter draws on its own overlay " +
            "canvas and never blocks taps.\n\n" +
            "Open the prefab to restyle or move the bar; its placement wins over the bar " +
            "placement in StabilitySettings. The Fill's size is driven at runtime, so style it " +
            "rather than resizing it. The caption text comes from localization.\n\n" +
            "Delete the prefab to go back to the code-built meter.", "OK");

        PingAsset(PrefabPath);
    }

    [MenuItem("TamalStacker/UI/Tremor Meter/Create Meter Prefab")]
    public static void CreatePrefab()
    {
        if (!CreatePrefabInternal(askBeforeReplace: true)) return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PingAsset(PrefabPath);
    }

    [MenuItem("TamalStacker/UI/Tremor Meter/Create Serpent's Edge Prefab")]
    public static void CreateEdgePrefab()
    {
        if (!CreateEdgePrefabInternal(askBeforeReplace: true)) return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PingAsset(EdgePrefabPath);
    }

    [MenuItem("TamalStacker/UI/Tremor Meter/Create Settings Asset")]
    public static void CreateSettings()
    {
        bool created = EnsureSettingsAsset();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PingAsset(SettingsPath);

        if (!created)
        {
            EditorUtility.DisplayDialog(DialogTitle, SettingsPath + " already exists - left untouched.", "OK");
        }
    }

    [MenuItem("TamalStacker/UI/Tremor Meter/Delete Meter Prefab")]
    public static void DeletePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            EditorUtility.DisplayDialog(DialogTitle, "There is no " + PrefabPath + " to delete.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(DialogTitle,
                "Delete " + PrefabPath + "?\n\nAny styling done to it will be lost. The game falls " +
                "back to the code-built meter.", "Delete", "Cancel"))
        {
            return;
        }

        AssetDatabase.DeleteAsset(PrefabPath);
        AssetDatabase.Refresh();
    }

    // ---- Settings ----

    /// <summary>Creates the settings asset with code defaults. Returns false if one already existed.</summary>
    private static bool EnsureSettingsAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<StabilitySettings>(SettingsPath) != null) return false;

        EnsureFolder("Assets", "Resources");
        var settings = ScriptableObject.CreateInstance<StabilitySettings>();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        return true;
    }

    // ---- Prefab ----

    private static bool CreatePrefabInternal(bool askBeforeReplace)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            (!askBeforeReplace || !EditorUtility.DisplayDialog(DialogTitle,
                PrefabPath + " already exists.\n\nReplace it? Any styling done to it will be lost.",
                "Replace", "Keep")))
        {
            return false;
        }

        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "UI");

        GameObject root = BuildHierarchy();
        try
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
            {
                Debug.LogError("[TowerStabilityUISetup] Failed to save " + PrefabPath);
                return false;
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return true;
    }

    private static bool CreateEdgePrefabInternal(bool askBeforeReplace)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EdgePrefabPath) != null &&
            (!askBeforeReplace || !EditorUtility.DisplayDialog(DialogTitle,
                EdgePrefabPath + " already exists.\n\nReplace it? Any styling done to it will be lost.",
                "Replace", "Keep")))
        {
            return false;
        }

        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "UI");

        var settings = AssetDatabase.LoadAssetAtPath<StabilitySettings>(SettingsPath);
        int sortingOrder;
        if (settings != null)
        {
            sortingOrder = settings.edgeCanvasSortingOrder;
        }
        else
        {
            var defaults = ScriptableObject.CreateInstance<StabilitySettings>();
            sortingOrder = defaults.edgeCanvasSortingOrder;
            Object.DestroyImmediate(defaults);
        }

        // Same builder the runtime fallback uses, so the prefab starts identical to it.
        var root = new GameObject("SerpentsEdge");
        try
        {
            RiskWindowView.BuildDefault(root, sortingOrder);
            PrefabUtility.SaveAsPrefabAsset(root, EdgePrefabPath, out bool success);
            if (!success)
            {
                Debug.LogError("[TowerStabilityUISetup] Failed to save " + EdgePrefabPath);
                return false;
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return true;
    }

    private static GameObject BuildHierarchy()
    {
        // Metrics come from the settings the code-built bar uses, so the prefab starts out
        // identical to what it replaces. A project asset wins over the code defaults.
        var settings = AssetDatabase.LoadAssetAtPath<StabilitySettings>(SettingsPath);
        StabilitySettings temp = null;
        if (settings == null)
        {
            temp = ScriptableObject.CreateInstance<StabilitySettings>();
            settings = temp;
        }

        var root = new GameObject("TowerStabilityMeter",
            typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(TowerStabilityView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the HUD and banners, below the boon picker (4000) and tutorial (4900).
        // Deliberately no GraphicRaycaster: a meter that ate taps would swallow the drop.
        canvas.sortingOrder = settings.canvasSortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        var group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        var bar = CreateChild("Bar", root.transform);
        Place(bar, settings.barAnchor, settings.barPosition, settings.barSize);
        var barImage = bar.gameObject.AddComponent<Image>();
        barImage.color = RunOverlayUI.Backdrop;
        barImage.raycastTarget = false;

        const float padding = 6f;
        var fill = CreateChild("Fill", bar);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(1f, 0f);
        fill.pivot = new Vector2(0.5f, 0f);
        fill.anchoredPosition = new Vector2(0f, padding);
        // 40% preview so there's something to style; overwritten at runtime.
        fill.sizeDelta = new Vector2(-2f * padding, (settings.barSize.y - 2f * padding) * 0.4f);
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = Color.Lerp(RunOverlayUI.Jade, RunOverlayUI.Gold, 0.4f / 0.6f);
        fillImage.raycastTarget = false;

        var preview = CreateChild("EdgePreview", bar);
        preview.anchorMin = new Vector2(0f, 0f);
        preview.anchorMax = new Vector2(1f, 0f);
        preview.pivot = new Vector2(0.5f, 0f);
        preview.anchoredPosition = new Vector2(0f, padding + (settings.barSize.y - 2f * padding) * 0.4f);
        preview.sizeDelta = new Vector2(-2f * padding, (settings.barSize.y - 2f * padding) * settings.edgeStrain);
        var previewImage = preview.gameObject.AddComponent<Image>();
        previewImage.color = new Color(0.79f, 0.64f, 0.29f, 0.6f);
        previewImage.raycastTarget = false;
        // Hidden until a Serpent's Edge drop is on offer; shown here only while editing.
        preview.gameObject.SetActive(false);

        var label = CreateLabel("Label", bar, "Tremor", 30f, RunOverlayUI.Parchment);
        Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(220f, 50f));

        var so = new SerializedObject(root.GetComponent<TowerStabilityView>());
        so.FindProperty("bar").objectReferenceValue = bar;
        so.FindProperty("fill").objectReferenceValue = fill;
        so.FindProperty("fillImage").objectReferenceValue = fillImage;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("previewFill").objectReferenceValue = preview;
        so.FindProperty("previewImage").objectReferenceValue = previewImage;
        so.FindProperty("fillPadding").floatValue = padding;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (temp != null) Object.DestroyImmediate(temp);

        return root;
    }

    // ---- Helpers ----

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void PingAsset(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (asset == null) return;
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
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
        label.textWrappingMode = TextWrappingModes.NoWrap;

        // The game's font rather than TMP's default, then the CJK switcher, which caches
        // whatever font it wakes up to - order matters.
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
