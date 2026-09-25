#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds the assets the power meter can use, without touching any existing UI or scene.
///
/// The power system runs without any of these (code defaults, code-built button); they are
/// here so tuning, art and sound can be authored:
///  - Resources/PowerSettings.asset       - meter + Jaguar Slam tuning, with the catalog.
///  - Powers/Power_&lt;Id&gt;.asset             - one per power: name/icon/colour/sound, wired into
///                                          the catalog (Jaguar Slam seeded with the Ground
///                                          Impact thud).
///  - Resources/UI/PowerMeter.prefab      - the meter button, identical to the code layout.
///  - Resources/UI/PowerLoadoutScreen.prefab - the pick-a-power screen (own menu item; not
///                                          made by Set Up Power Assets).
///
/// Non-destructive: existing assets are never overwritten, and an existing prefab is only
/// replaced after confirmation.
///
/// Menu: TamalStacker ▸ Powers ▸ ...
/// </summary>
public static class PowersSetup
{
    private const string SettingsPath = "Assets/Resources/" + PowerSettings.ResourcePath + ".asset";
    private const string PowersFolder = "Assets/ScriptableObjects/Powers";
    private const string JaguarSlamPath = PowersFolder + "/Power_JaguarSlam.asset";
    private const string PrefabPath = "Assets/Resources/" + PowerSystem.MeterPrefabResourcePath + ".prefab";
    private const string LoadoutPrefabPath = "Assets/Resources/" + PowerLoadoutView.PrefabResourcePath + ".prefab";
    private const string SlamSoundPath = "Assets/Art/Audio/Ground Impact/Ground Impact 1.mp3";
    private const string DialogTitle = "Powers";
    private const string IconFolder = "Assets/Resources/UI/Powers";
    private const string SoundFolder = "Assets/Art/Audio/Powers";

    [MenuItem("TamalStacker/Powers/Set Up Power Assets")]
    public static void SetUpAll()
    {
        bool settingsCreated = EnsureSettings(out PowerSettings settings);
        bool slamCreated = EnsureJaguarSlam(settings);
        int othersCreated = EnsureOtherPowers(settings);
        bool prefabCreated = CreatePrefabInternal(settings);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(DialogTitle,
            (settingsCreated ? "Created " : "Kept existing ") + SettingsPath + "\n" +
            (slamCreated ? "Created " : "Kept existing ") + JaguarSlamPath + "\n" +
            "Created " + othersCreated + " other power asset(s) in " + PowersFolder + "\n" +
            (prefabCreated ? "Created " : "Kept existing ") + PrefabPath + "\n\n" +
            "No scene or existing UI prefab was modified. The meter draws on its own overlay " +
            "canvas; delete the prefab to go back to the code-built button.\n\n" +
            "Icons and sounds: drop them in " + IconFolder + " and " + SoundFolder + ", then run " +
            "TamalStacker ▸ Powers ▸ Wire Power Art & Audio.\n\n" +
            "To test before beating the unlock temples: TamalStacker ▸ Powers ▸ Debug ▸ Unlock All Powers.",
            "OK");

        Selection.activeObject = settings;
    }

    [MenuItem("TamalStacker/Powers/Create Meter Prefab")]
    public static void CreatePrefab()
    {
        EnsureSettings(out PowerSettings settings);
        if (!CreatePrefabInternal(settings)) return;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
    }

    [MenuItem("TamalStacker/Powers/Delete Meter Prefab")]
    public static void DeletePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            EditorUtility.DisplayDialog(DialogTitle, "There is no " + PrefabPath + " to delete.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(DialogTitle,
                "Delete " + PrefabPath + "?\n\nAny styling done to it will be lost. The game falls " +
                "back to the code-built button.", "Delete", "Cancel"))
        {
            return;
        }

        AssetDatabase.DeleteAsset(PrefabPath);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Generates Resources/UI/PowerLoadoutScreen.prefab from the code layout, as a starting
    /// point to restyle. Edit the card template to style every card; colours and scales for the
    /// selected / other cards are on the root's PowerLoadoutView.
    /// </summary>
    [MenuItem("TamalStacker/Powers/Create Loadout Screen Prefab")]
    public static void CreateLoadoutPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LoadoutPrefabPath) != null &&
            !EditorUtility.DisplayDialog(DialogTitle,
                LoadoutPrefabPath + " already exists.\n\nReplace it? Any styling done to it will be lost.",
                "Replace", "Keep"))
        {
            return;
        }

        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "UI");

        // Same builder the runtime fallback uses, so the prefab starts identical to it.
        var root = new GameObject("PowerLoadoutScreen");
        try
        {
            PowerLoadoutView.BuildDefault(root);
            PrefabUtility.SaveAsPrefabAsset(root, LoadoutPrefabPath, out bool success);
            if (!success)
            {
                Debug.LogError("[PowersSetup] Failed to save " + LoadoutPrefabPath);
                return;
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LoadoutPrefabPath);
        EditorGUIUtility.PingObject(prefab);
        Selection.activeObject = prefab;

        EditorUtility.DisplayDialog(DialogTitle,
            "Created " + LoadoutPrefabPath + ".\n\n" +
            "- Style Panel/Cards/CardTemplate to style every card (add a child and assign it as " +
            "the card's Selected Marker for a glow on the picked one).\n" +
            "- Selected / other card colours and scale are on the root's PowerLoadoutView.\n" +
            "- Texts are placeholders; they are filled from localization at runtime.\n\n" +
            "To see it: TamalStacker ▸ Powers ▸ Debug ▸ Unlock All Powers, then Play from the main " +
            "menu and tap Infinite. Delete the prefab to go back to the code-built screen.",
            "OK");
    }

    [MenuItem("TamalStacker/Powers/Delete Loadout Screen Prefab")]
    public static void DeleteLoadoutPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LoadoutPrefabPath) == null)
        {
            EditorUtility.DisplayDialog(DialogTitle, "There is no " + LoadoutPrefabPath + " to delete.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(DialogTitle,
                "Delete " + LoadoutPrefabPath + "?\n\nAny styling done to it will be lost. The game falls " +
                "back to the code-built screen.", "Delete", "Cancel"))
        {
            return;
        }

        AssetDatabase.DeleteAsset(LoadoutPrefabPath);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Assigns each power's icon (Resources/UI/Powers/Power_&lt;Id&gt;_Icon) and fire sound
    /// (Art/Audio/Powers/SFX_Power_&lt;Id&gt;) by file name, fixing their import settings.
    /// </summary>
    [MenuItem("TamalStacker/Powers/Wire Power Art & Audio")]
    public static void WirePowerArt()
    {
        EnsureSettings(out PowerSettings settings);
        EnsureJaguarSlam(settings);
        EnsureOtherPowers(settings);
        TempleRulesSetup.EnsureFolder(IconFolder);
        TempleRulesSetup.EnsureFolder(SoundFolder);

        var wired = new System.Collections.Generic.List<string>();
        var missing = new System.Collections.Generic.List<string>();

        foreach (PowerId id in System.Enum.GetValues(typeof(PowerId)))
        {
            var def = AssetDatabase.LoadAssetAtPath<PowerDefinition>(PowerAssetPath(id));
            if (def == null) continue;

            string iconName = "Power_" + id + "_Icon";
            Sprite icon = TempleRulesSetup.FindSprite(IconFolder, iconName);
            if (icon != null) { def.icon = icon; wired.Add(iconName); }
            else if (def.icon == null) missing.Add(IconFolder + "/" + iconName);

            string soundName = "SFX_Power_" + id;
            AudioClip clip = TempleRulesSetup.FindClip(SoundFolder, soundName);
            if (clip != null) { def.fireSound = clip; wired.Add(soundName); }
            else if (def.fireSound == null) missing.Add(SoundFolder + "/" + soundName);

            EditorUtility.SetDirty(def);
        }

        // Shared medallion art and the Quetzal tint are loaded from Resources by name at
        // runtime; only their import settings need fixing here.
        foreach (string shared in new[] { "PowerMedallion_Base", "PowerMedallion_Ring", "Power_QuetzalFeather_Vignette" })
        {
            if (TempleRulesSetup.FindSprite(IconFolder, shared) != null) wired.Add(shared);
            else if (shared.EndsWith("Vignette")) missing.Add(IconFolder + "/" + shared + " (optional)");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(DialogTitle, TempleRulesSetup.Report(wired, missing), "OK");
    }

    private static string PowerAssetPath(PowerId id) => PowersFolder + "/Power_" + id + ".asset";

    /// <summary>Creates the asset for every power after Jaguar Slam that doesn't have one yet.</summary>
    private static int EnsureOtherPowers(PowerSettings settings)
    {
        int created = 0;
        foreach (PowerId id in System.Enum.GetValues(typeof(PowerId)))
        {
            if (id == PowerId.JaguarSlam) continue;

            string path = PowerAssetPath(id);
            var def = AssetDatabase.LoadAssetAtPath<PowerDefinition>(path);
            if (def == null)
            {
                EnsureFolder("Assets", "ScriptableObjects");
                EnsureFolder("Assets/ScriptableObjects", "Powers");
                def = PowerDefinition.CreateDefault(id);
                AssetDatabase.CreateAsset(def, path);
                created++;
            }

            // Only ever adds to the catalog.
            if (settings != null && !settings.powers.Contains(def))
            {
                settings.powers.Add(def);
                EditorUtility.SetDirty(settings);
            }
        }
        return created;
    }

    // ---- Debug ----

    [MenuItem("TamalStacker/Powers/Debug/Unlock All Powers")]
    public static void DebugUnlockAll()
    {
        foreach (PowerId id in System.Enum.GetValues(typeof(PowerId))) PowerUnlocks.DebugSetUnlocked(id, true);
        Debug.Log("[PowersSetup] All powers unlocked (takes effect on the next run). Pick one on the " +
                  "loadout screen that opens when you tap Play in the main menu.");
    }

    [MenuItem("TamalStacker/Powers/Debug/Unlock Jaguar Slam")]
    public static void DebugUnlock()
    {
        PowerUnlocks.DebugSetUnlocked(PowerId.JaguarSlam, true);
        Debug.Log("[PowersSetup] Jaguar Slam unlocked (takes effect on the next run).");
    }

    [MenuItem("TamalStacker/Powers/Debug/Lock All Powers + Replay Intros")]
    public static void DebugLock()
    {
        PowerUnlocks.ResetAll();
        Debug.Log("[PowersSetup] Power unlocks, intros and the equipped pick cleared. Note: a completed " +
                  "unlock temple (2 / 5 / 8 / 11) grants its power straight back - reset level progress " +
                  "to test the unlock itself.");
    }

    // ---- Assets ----

    private static bool EnsureSettings(out PowerSettings settings)
    {
        settings = AssetDatabase.LoadAssetAtPath<PowerSettings>(SettingsPath);
        if (settings != null) return false;

        EnsureFolder("Assets", "Resources");
        settings = ScriptableObject.CreateInstance<PowerSettings>();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        return true;
    }

    private static bool EnsureJaguarSlam(PowerSettings settings)
    {
        var slam = AssetDatabase.LoadAssetAtPath<PowerDefinition>(JaguarSlamPath);
        bool created = false;

        if (slam == null)
        {
            EnsureFolder("Assets", "ScriptableObjects");
            EnsureFolder("Assets/ScriptableObjects", "Powers");

            slam = PowerDefinition.CreateDefault(PowerId.JaguarSlam);
            slam.fireSound = AssetDatabase.LoadAssetAtPath<AudioClip>(SlamSoundPath);
            AssetDatabase.CreateAsset(slam, JaguarSlamPath);
            created = true;
        }

        // Wire into the catalog if it isn't there - the only change ever made to an existing
        // settings asset, and it only adds.
        if (settings != null && !settings.powers.Contains(slam))
        {
            settings.powers.Add(slam);
            EditorUtility.SetDirty(settings);
        }

        return created;
    }

    private static bool CreatePrefabInternal(PowerSettings settings)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog(DialogTitle,
                PrefabPath + " already exists.\n\nReplace it? Any styling done to it will be lost.",
                "Replace", "Keep"))
        {
            return false;
        }

        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "UI");

        // Same builder the runtime fallback uses, so the prefab starts identical to it.
        var root = new GameObject("PowerMeter");
        try
        {
            PowerMeterView.BuildDefault(root, settings != null ? settings : PowerSettings.Current);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
            {
                Debug.LogError("[PowersSetup] Failed to save " + PrefabPath);
                return false;
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return true;
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
