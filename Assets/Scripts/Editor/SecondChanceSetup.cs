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
            note += $"Kept {PrefabPath}.";
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
        Place(panel, new Vector2(0.5f, 0f), new Vector2(0f, 560f), new Vector2(880f, 560f));
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = RunOverlayUI.Jade;
        UIHouseStyle.ApplyImage(panelImage, UIHouseStyle.Primary);

        var title = CreateLabel("Title", panel, "Close one!", 56f, RunOverlayUI.Gold);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(800f, 90f));

        var sub = CreateLabel("Sub", panel, "Kukulkan offers a second chance", 36f, RunOverlayUI.Parchment);
        Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(800f, 90f));

        var timer = CreateChild("Timer", panel);
        Place(timer, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(700f, 14f));
        var timerImage = timer.gameObject.AddComponent<Image>();
        timerImage.color = RunOverlayUI.Gold;
        timerImage.type = Image.Type.Filled;
        timerImage.fillMethod = Image.FillMethod.Horizontal;
        timerImage.raycastTarget = false;

        var accept = CreateButton("AcceptButton", panel, UIHouseStyle.Primary,
            new Vector2(0f, 40f), "Watch to try that stone again", 38f, out var acceptLabel);
        var decline = CreateButton("DeclineButton", panel, UIHouseStyle.Secondary,
            new Vector2(0f, -170f), "Let it fall", 34f, out var declineLabel);
        // Anchored at the panel's centre-bottom half: accept above, decline below.
        accept.GetComponent<RectTransform>().anchorMin = accept.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
        decline.GetComponent<RectTransform>().anchorMin = decline.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);

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

    private static Button CreateButton(string name, Transform parent, string style,
        Vector2 position, string text, float size, out TextMeshProUGUI label)
    {
        var rect = CreateChild(name, parent);
        Place(rect, new Vector2(0.5f, 0.5f), position, new Vector2(760f, 120f));
        var image = rect.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(image, style);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UIHouseStyle.ApplyButton(button, style);

        label = CreateLabel("Label", rect, text, size, RunOverlayUI.Parchment);
        Stretch(label.rectTransform);
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
