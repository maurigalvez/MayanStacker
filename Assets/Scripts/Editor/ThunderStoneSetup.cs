#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Brings the Chaac's Thunderstone online in an existing project: adds it to the
/// BlockVarietyTable asset (keeping every existing entry's tuning) and lists it on the storm
/// rituals. New calendars get the same lists from DailyRitualSetup.
///
/// Idempotent and prompt-free. Menu: TamalStacker ▸ Run Content ▸ Add Thunderstone.
/// </summary>
public static class ThunderStoneSetup
{
    private const string RitualFolder = "Assets/Resources/DailyChallenge/Rituals";
    private const string TablePath = "Assets/Resources/" + BlockVarietyTable.ResourcePath + ".asset";

    // The rain-and-storm rituals: Chaac's trial, Chaac's flood, the storm serpent.
    private static readonly string[] RitualIds = { "chaacs_trial", "flood_of_chaac", "storm_serpent" };

    [MenuItem("TamalStacker/Run Content/Add Thunderstone")]
    public static void AddThunderstone() => AddThunderstoneBatch();

    public static void AddThunderstoneBatch()
    {
        var table = AssetDatabase.LoadAssetAtPath<BlockVarietyTable>(TablePath);
        if (table == null)
        {
            Debug.LogWarning("[ThunderStoneSetup] No BlockVarietyTable asset. Run TamalStacker > Run Content > Create Missing Assets first.");
        }
        else if (table.AddMissingDefaults())
        {
            EditorUtility.SetDirty(table);
            Debug.Log("[ThunderStoneSetup] Added missing block variants to " + TablePath);
        }
        else
        {
            Debug.Log("[ThunderStoneSetup] Kept " + TablePath + " (variants already present)");
        }

        foreach (string id in RitualIds)
        {
            string path = $"{RitualFolder}/Ritual_{id}.asset";
            var ritual = AssetDatabase.LoadAssetAtPath<DailyRitual>(path);
            if (ritual == null)
            {
                Debug.LogWarning("[ThunderStoneSetup] Missing ritual asset " + path);
                continue;
            }

            if (ritual.specialBlocks == null) ritual.specialBlocks = new List<BlockVariantId>();
            if (ritual.specialBlocks.Contains(BlockVariantId.ThunderStone))
            {
                Debug.Log("[ThunderStoneSetup] Kept " + path);
                continue;
            }

            ritual.specialBlocks.Add(BlockVariantId.ThunderStone);
            EditorUtility.SetDirty(ritual);
            Debug.Log("[ThunderStoneSetup] Added Thunderstone to " + path);
        }

        AssetDatabase.SaveAssets();
    }
}
#endif
