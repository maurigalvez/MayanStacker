#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates Second Chance: Resources/SecondChanceSettings.asset (rules + switch) and
/// Resources/UI/SecondChance.prefab (the offer panel, styled through <see cref="UIHouseStyle"/>).
/// Idempotent: existing assets are kept, so hand styling and tuning survive a re-run.
///
/// Menu: TamalStacker ▸ UI ▸ Create Second Chance Assets
/// </summary>
public static class SecondChanceSetup
{
    private const string SettingsPath = "Assets/Resources/" + SecondChanceSettings.ResourcePath + ".asset";
    private const string PrefabPath = "Assets/Resources/UI/SecondChance.prefab";

    [MenuItem("TamalStacker/UI/Create Second Chance Assets")]
    public static void CreateAssets()
    {
        EditorUtility.DisplayDialog("Second Chance", Ensure(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static void CreateAssetsBatch()
    {
        Ensure();
    }

    private static string Ensure()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");

        string note = string.Empty;

        if (AssetDatabase.LoadAssetAtPath<SecondChanceSettings>(SettingsPath) != null)
        {
            note += $"Kept {SettingsPath}. ";
        }
        else
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SecondChanceSettings>(), SettingsPath);
            note += $"Created {SettingsPath}. ";
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            note += $"Kept {PrefabPath}. " + EnsureReadableLayout();
        }
        else
        {
            GameObject root = BuildHierarchy();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            note += $"Created {PrefabPath}.";
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SecondChanceSetup] {note}");
        return note;
    }

    private static GameObject BuildHierarchy()
    {
        var root = new GameObject("SecondChance",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SecondChanceView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SecondChanceView.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // The panel sits in the lower third, clear of the tower; the rest of the canvas is inert.
        var panel = CreateChild("Panel", root.transform);
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = RunOverlayUI.Jade;
        UIHouseStyle.ApplyImage(panelImage, UIHouseStyle.Primary);

        var title = CreateLabel("Title", panel, "Close one!", 84f, RunOverlayUI.Gold);
        var sub = CreateLabel("Sub", panel, "Kukulkan offers a second chance", 50f, RunOverlayUI.Parchment);

        var timer = CreateChild("Timer", panel);
        var timerImage = timer.gameObject.AddComponent<Image>();
        timerImage.color = RunOverlayUI.Gold;
        timerImage.type = Image.Type.Filled;
        timerImage.fillMethod = Image.FillMethod.Horizontal;
        timerImage.raycastTarget = false;

        var accept = CreateButton("AcceptButton", panel, UIHouseStyle.Primary,
            "Watch to try that stone again", 52f, out var acceptLabel);
        var decline = CreateButton("DeclineButton", panel, UIHouseStyle.Secondary,
            "Let it fall", 48f, out var declineLabel);
        LayOut(panel);

        var so = new SerializedObject(root.GetComponent<SecondChanceView>());
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("subLabel").objectReferenceValue = sub;
        so.FindProperty("acceptButton").objectReferenceValue = accept;
        so.FindProperty("acceptLabel").objectReferenceValue = acceptLabel;
        so.FindProperty("declineButton").objectReferenceValue = decline;
        so.FindProperty("declineLabel").objectReferenceValue = declineLabel;
        so.FindProperty("timerFill").objectReferenceValue = timerImage;
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    /// <summary>
    /// Re-lays an existing panel at the readable sizes. Only positions, sizes and font sizes
    /// change; art, colours and wiring are kept.
    /// </summary>
    private static string EnsureReadableLayout()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var panel = root.transform.Find("Panel") as RectTransform;
            if (panel == null) return "Layout skipped (prefab has no Panel).";
            LayOut(panel);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "Enlarged the panel and its text.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Title, line, timer, then the two buttons, all large enough to read at a glance mid-run.
    /// Text auto-sizes down for long translations but never under the house minimum.
    /// </summary>
    private static void LayOut(RectTransform panel)
    {
        Place(panel, new Vector2(0.5f, 0f), new Vector2(0f, 600f), new Vector2(980f, 720f));

        PlaceLabel(panel, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(880f, 110f), 84f);
        PlaceLabel(panel, "Sub", new Vector2(0.5f, 1f), new Vector2(0f, -195f), new Vector2(880f, 80f), 50f);

        var timer = panel.Find("Timer") as RectTransform;
        if (timer != null) Place(timer, new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(800f, 18f));

        var accept = panel.Find("AcceptButton") as RectTransform;
        if (accept != null)
        {
            Place(accept, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(860f, 160f));
            PlaceLabel(accept, "Label", Vector2.zero, Vector2.zero, Vector2.zero, 52f);
            Stretch(accept.Find("Label") as RectTransform, 40f);
        }

        var decline = panel.Find("DeclineButton") as RectTransform;
        if (decline != null)
        {
            Place(decline, new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(860f, 140f));
            PlaceLabel(decline, "Label", Vector2.zero, Vector2.zero, Vector2.zero, 48f);
            Stretch(decline.Find("Label") as RectTransform, 40f);
        }
    }

    /// <summary>Sizes a child label (and places it when <paramref name="size"/> is non-zero).</summary>
    private static void PlaceLabel(RectTransform parent, string name, Vector2 anchor, Vector2 position,
        Vector2 size, float fontSize)
    {
        var label = parent.Find(name)?.GetComponent<TextMeshProUGUI>();
        if (label == null) return;
        if (size != Vector2.zero) Place(label.rectTransform, anchor, position, size);
        label.enableAutoSizing = true;
        label.fontSizeMax = fontSize;
        label.fontSizeMin = UIHouseStyle.MinFontSize;
        label.fontSize = fontSize;
        UIHouseStyle.EnforceMinFontSize(label);
    }

    private static Button CreateButton(string name, Transform parent, string style,
        string text, float size, out TextMeshProUGUI label)
    {
        var rect = CreateChild(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(image, style);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UIHouseStyle.ApplyButton(button, style);

        label = CreateLabel("Label", rect, text, size, RunOverlayUI.Parchment);
        Stretch(label.rectTransform, 40f);
        return button;
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
        if (RunOverlayUI.DisplayFont != null) label.font = RunOverlayUI.DisplayFont;
        else if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        rt.gameObject.AddComponent<LocaleFontSwitcher>();
        return label;
    }

    /// <summary>Fills the parent, <paramref name="inset"/> clear of its left and right edges.</summary>
    private static void Stretch(RectTransform rt, float inset)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, 0f);
        rt.offsetMax = new Vector2(-inset, 0f);
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
