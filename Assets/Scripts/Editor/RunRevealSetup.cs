#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the post-run Offering reveal: Resources/RunRevealSettings.asset (bonus table + switch)
/// and Resources/UI/RunReveal.prefab (the chip, styled through <see cref="UIHouseStyle"/>).
/// Idempotent: existing assets are kept, so hand styling and tuning survive a re-run.
///
/// Menu: TamalStacker ▸ UI ▸ Create Run Reveal Assets
/// </summary>
public static class RunRevealSetup
{
    private const string SettingsPath = "Assets/Resources/" + RunRevealSettings.ResourcePath + ".asset";
    private const string PrefabPath = "Assets/Resources/UI/RunReveal.prefab";

    [MenuItem("TamalStacker/UI/Create Run Reveal Assets")]
    public static void CreateAssets()
    {
        EditorUtility.DisplayDialog("Run reveal", Ensure(), "OK");
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

        if (AssetDatabase.LoadAssetAtPath<RunRevealSettings>(SettingsPath) != null)
        {
            note += $"Kept {SettingsPath}. ";
        }
        else
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<RunRevealSettings>(), SettingsPath);
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
        Debug.Log($"[RunRevealSetup] {note}");
        return note;
    }

    private static GameObject BuildHierarchy()
    {
        var root = new GameObject("RunReveal",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RunRevealView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = RunRevealView.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // The chip is the only raycast target: the rest of the canvas lets taps through.
        var chip = CreateChild("Chip", root.transform);
        Place(chip, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(640f, 170f));
        var image = chip.gameObject.AddComponent<Image>();
        image.color = RunOverlayUI.Jade;
        UIHouseStyle.ApplyImage(image, UIHouseStyle.Primary);
        var button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UIHouseStyle.ApplyButton(button, UIHouseStyle.Primary);

        var prompt = CreateLabel("Prompt", chip, "Tap to open the Offering", 38f, RunOverlayUI.Parchment);
        Stretch(prompt.rectTransform);

        var tier = CreateLabel("Tier", chip, "Blessed", 40f, RunOverlayUI.Gold);
        Place(tier.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), new Vector2(600f, 60f));

        var amount = CreateLabel("Amount", chip, "+3 jade", 44f, RunOverlayUI.Parchment);
        Place(amount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(600f, 60f));

        var so = new SerializedObject(root.GetComponent<RunRevealView>());
        so.FindProperty("chip").objectReferenceValue = chip;
        so.FindProperty("chipButton").objectReferenceValue = button;
        so.FindProperty("promptLabel").objectReferenceValue = prompt;
        so.FindProperty("tierLabel").objectReferenceValue = tier;
        so.FindProperty("amountLabel").objectReferenceValue = amount;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
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
