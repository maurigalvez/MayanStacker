#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the daily login chest:
///   • Resources/LoginChestSettings.asset - the reward cycle. Kept if it exists, so tuning survives.
///   • Resources/UI/LoginChest.prefab - the panel (<see cref="LoginChestView"/>), styled through
///     <see cref="UIHouseStyle"/>. Kept if it exists, so hand styling survives a re-run.
///   • a "CHEST" chip (<see cref="LoginChestMenuChip"/>) on the Main Menu panel inside the Main Menu
///     Canvas prefab, under the Missions chip. Skipped if one already exists.
///
/// Menus: TamalStacker ▸ Economy ▸ Create Login Chest Settings / Add Login Chest Menu Chip,
/// TamalStacker ▸ UI ▸ Create Login Chest Prefab.
/// </summary>
public static class LoginChestSetup
{
    private const string SettingsPath = "Assets/Resources/" + LoginChestSettings.ResourcePath + ".asset";
    private const string PrefabPath = "Assets/Resources/UI/LoginChest.prefab";
    private const string MainMenuPrefabPath = "Assets/Prefabs/Main Menu/Main Menu Canvas.prefab";
    private const string ChipName = "LoginChestChip";

    // Directly under the Missions chip (-500, 120 tall) with a 20 px gap.
    private static readonly Vector2 ChipPosition = new Vector2(-30f, -640f);
    private static readonly Vector2 ChipSize = new Vector2(180f, 120f);

    [MenuItem("TamalStacker/Economy/Create Login Chest Settings")]
    public static void CreateSettings()
    {
        EditorUtility.DisplayDialog("Login chest", EnsureSettings(), "OK");
    }

    [MenuItem("TamalStacker/UI/Create Login Chest Prefab")]
    public static void CreatePrefab()
    {
        EditorUtility.DisplayDialog("Login chest", EnsurePrefab(), "OK");
    }

    [MenuItem("TamalStacker/Economy/Add Login Chest Menu Chip")]
    public static void AddMenuChip()
    {
        EditorUtility.DisplayDialog("Login chest", EnsureChip(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI: settings, prefab and menu chip.</summary>
    public static void CreateAllBatch()
    {
        EnsureSettings();
        EnsurePrefab();
        EnsureChip();
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");
    }

    private static string EnsureSettings()
    {
        EnsureFolders();
        string note;
        if (AssetDatabase.LoadAssetAtPath<LoginChestSettings>(SettingsPath) != null)
        {
            note = $"Kept {SettingsPath}.";
        }
        else
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LoginChestSettings>(), SettingsPath);
            note = $"Created {SettingsPath}.";
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[LoginChestSetup] {note}");
        return note;
    }

    private static string EnsurePrefab()
    {
        EnsureFolders();
        string note;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            note = $"Kept {PrefabPath}.";
        }
        else
        {
            GameObject root = BuildPanel();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            note = $"Created {PrefabPath}.";
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LoginChestSetup] {note}");
        return note;
    }

    private static GameObject BuildPanel()
    {
        var root = new GameObject("LoginChest",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LoginChestView));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = LoginChestView.SortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        var panel = RunOverlayUI.CreateChild("Panel", root.transform);
        RunOverlayUI.Stretch(panel);
        var dim = panel.gameObject.AddComponent<Image>();
        dim.color = RunOverlayUI.Backdrop;
        dim.raycastTarget = true;

        var modal = RunOverlayUI.CreateChild("Modal", panel);
        RunOverlayUI.Place(modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 940f));
        var slab = modal.gameObject.AddComponent<Image>();
        slab.color = RunOverlayUI.Stone;
        UIHouseStyle.ApplyImage(slab, UIHouseStyle.Modal);

        var title = Label("Title", modal, "Kukulkan's Chest", 60f, RunOverlayUI.Gold, UIHouseStyle.Title);
        RunOverlayUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(780f, 90f));

        var day = Label("Day", modal, "Day 1 of 7", 40f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        RunOverlayUI.Place(day.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(780f, 60f));

        var reward = Label("Reward", modal, "+10 jade", 84f, RunOverlayUI.Gold, UIHouseStyle.Title);
        reward.enableAutoSizing = true;
        reward.fontSizeMin = 40f;
        reward.fontSizeMax = 84f;
        RunOverlayUI.Place(reward.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(780f, 140f));

        var next = Label("Next", modal, "Tomorrow: +15 jade", 36f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        next.enableAutoSizing = true;
        next.fontSizeMin = 22f;
        next.fontSizeMax = 36f;
        RunOverlayUI.Place(next.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(780f, 70f));

        var claim = RunOverlayUI.CreateChild("ClaimButton", modal);
        RunOverlayUI.Place(claim, new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(560f, 130f));
        var claimImage = claim.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(claimImage, UIHouseStyle.Primary);
        var claimButton = claim.gameObject.AddComponent<Button>();
        claimButton.targetGraphic = claimImage;
        UIHouseStyle.ApplyButton(claimButton, UIHouseStyle.Primary);
        var claimLabel = Label("Label", claim, "Open", 46f, RunOverlayUI.Parchment, UIHouseStyle.Title);
        RunOverlayUI.Stretch(claimLabel.rectTransform);

        var close = RunOverlayUI.CreateChild("CloseButton", modal);
        RunOverlayUI.Place(close, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(420f, 100f));
        var closeImage = close.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(closeImage, UIHouseStyle.Secondary);
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        UIHouseStyle.ApplyButton(closeButton, UIHouseStyle.Secondary);
        var closeLabel = Label("Label", close, "Close", 38f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        RunOverlayUI.Stretch(closeLabel.rectTransform);

        var so = new SerializedObject(root.GetComponent<LoginChestView>());
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("dayLabel").objectReferenceValue = day;
        so.FindProperty("rewardLabel").objectReferenceValue = reward;
        so.FindProperty("nextLabel").objectReferenceValue = next;
        so.FindProperty("claimButton").objectReferenceValue = claimButton;
        so.FindProperty("claimLabel").objectReferenceValue = claimLabel;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("closeLabel").objectReferenceValue = closeLabel;
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, string role)
    {
        var label = RunOverlayUI.CreateLabel(name, parent, text, size, color);
        UIHouseStyle.ApplyLabel(label, role);
        label.color = color;
        label.fontSize = size;
        return label;
    }

    private static string EnsureChip()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath) == null)
        {
            string missing = MainMenuPrefabPath + " was not found.";
            Debug.LogError($"[LoginChestSetup] {missing}");
            return missing;
        }

        string result = null;
        GameObject root = PrefabUtility.LoadPrefabContents(MainMenuPrefabPath);
        try
        {
            result = BuildChip(root);
            if (result == null)
            {
                PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
                result = $"Added {ChipName} to {MainMenuPrefabPath}.";
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[LoginChestSetup] {result}");
        return result;
    }

    /// <returns>Null on success, or the reason nothing was built.</returns>
    private static string BuildChip(GameObject root)
    {
        var menu = root.GetComponentInChildren<MainMenuManager>(true);
        var panel = menu != null
            ? new SerializedObject(menu).FindProperty("mainMenuPanel").objectReferenceValue as GameObject
            : null;
        if (panel == null) return "The prefab has no Main Menu panel (MainMenuManager.mainMenuPanel is unassigned).";
        if (panel.transform.Find(ChipName) != null) return $"Kept existing {ChipName}.";

        var chip = RunOverlayUI.CreateChild(ChipName, panel.transform);
        chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f);
        chip.pivot = new Vector2(1f, 1f);
        chip.anchoredPosition = ChipPosition;
        chip.sizeDelta = ChipSize;
        chip.SetAsLastSibling();

        var image = chip.gameObject.AddComponent<Image>();
        UIHouseStyle.ApplyImage(image, UIHouseStyle.Secondary);
        var button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UIHouseStyle.ApplyButton(button, UIHouseStyle.Secondary);

        var title = Label("Label", chip, "CHEST", 30f, RunOverlayUI.Gold, UIHouseStyle.Title);
        title.enableAutoSizing = true;
        title.fontSizeMin = 18f;
        title.fontSizeMax = 32f;
        title.enableWordWrapping = false;
        title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(8f, 0f);
        title.rectTransform.offsetMax = new Vector2(-8f, -8f);
        var localized = title.gameObject.AddComponent<LocalizedText>();
        var localizedSo = new SerializedObject(localized);
        var keyProp = localizedSo.FindProperty("localizationKey");
        if (keyProp != null)
        {
            keyProp.stringValue = "login_chest_chip";
            localizedSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var status = Label("Status", chip, "Ready!", 28f, RunOverlayUI.Parchment, UIHouseStyle.Body);
        status.enableAutoSizing = true;
        status.fontSizeMin = 16f;
        status.fontSizeMax = 30f;
        status.enableWordWrapping = false;
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        status.rectTransform.offsetMin = new Vector2(8f, 8f);
        status.rectTransform.offsetMax = new Vector2(-8f, 0f);

        chip.gameObject.AddComponent<CanvasGroup>();
        var chipView = chip.gameObject.AddComponent<LoginChestMenuChip>();
        var so = new SerializedObject(chipView);
        so.FindProperty("statusLabel").objectReferenceValue = status;
        so.ApplyModifiedPropertiesWithoutUndo();
        return null;
    }
}
#endif
