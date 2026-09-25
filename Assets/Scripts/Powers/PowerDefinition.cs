using UnityEngine;

/// <summary>
/// Every power the player can earn. The names are recorded by analytics (power_unlocked,
/// power_used), so rename with care.
/// </summary>
public enum PowerId
{
    JaguarSlam = 0,
    QuetzalFeather = 1,
    ObsidianBlade = 2,
    KukulkansCall = 3
}

/// <summary>
/// One tap-to-fire power: what it's called, how it looks, and what unlocks it.
///
/// Powers are charged by play only (Perfect landings fill the meter) — nothing that affects
/// gameplay is ever sold in a ranked mode. What the power *does* lives in
/// <see cref="PowerSystem"/>; this asset only carries presentation and unlock data, so a
/// designer can restyle or re-gate a power without touching code.
///
/// Missing entirely, <see cref="PowerSettings"/> builds a code default for each power, so the
/// system works before any asset is created.
/// </summary>
[CreateAssetMenu(fileName = "Power_", menuName = "TamalStacker/Power Definition", order = 6)]
public class PowerDefinition : ScriptableObject
{
    public PowerId id = PowerId.JaguarSlam;

    [Header("Copy (localization keys)")]
    public string nameKey = "power_jaguar_slam_name";
    public string descriptionKey = "power_jaguar_slam_desc";

    [Header("Look")]
    [Tooltip("Icon in the power medallion. Null draws the localized name instead.")]
    public Sprite icon;

    [Tooltip("Button and banner colour for this power.")]
    public Color accentColor = RunOverlayUI.Gold;

    [Tooltip("Played when the power fires. Null plays nothing extra.")]
    public AudioClip fireSound;

    [Range(0f, 1f)]
    public float fireSoundVolume = 1f;

    [Header("Unlock")]
    [Tooltip("Completing this temple (by levelNumber) for the first time unlocks the power. " +
             "0 = unlocked from the start.")]
    [Min(0)]
    public int unlockedByLevel = 2;

    /// <summary>Where each power's default icon lives under Resources.</summary>
    public static string IconResourcePath(PowerId powerId) => "UI/Powers/Power_" + powerId + "_Icon";

    /// <summary>Code default used when no asset exists for <paramref name="powerId"/>.</summary>
    public static PowerDefinition CreateDefault(PowerId powerId)
    {
        var def = CreateInstance<PowerDefinition>();
        def.id = powerId;

        switch (powerId)
        {
            case PowerId.QuetzalFeather:
                def.name = "Power_QuetzalFeather";
                def.nameKey = "power_quetzal_feather_name";
                def.descriptionKey = "power_quetzal_feather_desc";
                def.accentColor = new Color(0.25f, 0.78f, 0.55f, 1f); // quetzal green
                def.unlockedByLevel = 5; // Uxbenka
                break;

            case PowerId.ObsidianBlade:
                def.name = "Power_ObsidianBlade";
                def.nameKey = "power_obsidian_blade_name";
                def.descriptionKey = "power_obsidian_blade_desc";
                def.accentColor = new Color(0.62f, 0.56f, 0.86f, 1f); // obsidian sheen
                def.unlockedByLevel = 8; // Muyil
                break;

            case PowerId.KukulkansCall:
                def.name = "Power_KukulkansCall";
                def.nameKey = "power_kukulkans_call_name";
                def.descriptionKey = "power_kukulkans_call_desc";
                def.accentColor = new Color(0.3f, 0.82f, 0.8f, 1f); // feathered-serpent teal
                def.unlockedByLevel = 11; // Altun Ha
                break;

            case PowerId.JaguarSlam:
            default:
                def.name = "Power_JaguarSlam";
                def.nameKey = "power_jaguar_slam_name";
                def.descriptionKey = "power_jaguar_slam_desc";
                def.accentColor = RunOverlayUI.Gold;
                def.unlockedByLevel = 2; // Dzibilchaltún
                break;
        }

        def.icon = Resources.Load<Sprite>(IconResourcePath(powerId));

        return def;
    }
}
