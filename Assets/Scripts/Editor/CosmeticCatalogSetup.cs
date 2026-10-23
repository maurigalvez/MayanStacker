using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/CosmeticCatalog.asset with the launch cosmetics (Kukulkan shift colours and
/// landing-burst colours). Idempotent: an existing asset is kept as tuned. Prices are proposals.
///
/// Menu: TamalStacker ▸ Economy ▸ Create Cosmetic Catalog
/// </summary>
public static class CosmeticCatalogSetup
{
    private const string CatalogPath = "Assets/Resources/" + CosmeticCatalog.ResourcePath + ".asset";

    [MenuItem("TamalStacker/Economy/Create Cosmetic Catalog")]
    public static void CreateCatalog()
    {
        EditorUtility.DisplayDialog("Cosmetic catalog", EnsureCatalog(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static void CreateCatalogBatch()
    {
        EnsureCatalog();
    }

    private static string EnsureCatalog()
    {
        if (AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CatalogPath) != null)
        {
            string kept = $"CosmeticCatalog already exists - kept as tuned ({CatalogPath}).";
            Debug.Log($"[CosmeticCatalogSetup] {kept}");
            return kept;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
        catalog.items = new[]
        {
            Shift("shift_gold", "cosmetic_shift_gold", 0, new Color(1f, 0.85f, 0.45f), keep: true),
            Shift("shift_jade", "cosmetic_shift_jade", 60, new Color(0.30f, 0.85f, 0.55f)),
            Shift("shift_turquoise", "cosmetic_shift_turquoise", 90, new Color(0.25f, 0.80f, 0.85f)),
            Shift("shift_ember", "cosmetic_shift_ember", 120, new Color(1f, 0.45f, 0.25f)),
            Shift("shift_obsidian", "cosmetic_shift_obsidian", 150, new Color(0.62f, 0.40f, 0.95f)),

            Burst("burst_theme", "cosmetic_burst_theme", 0, Color.white, Color.white, keep: true),
            Burst("burst_jade", "cosmetic_burst_jade", 40, new Color(0.25f, 0.80f, 0.50f), new Color(0.65f, 0.95f, 0.70f)),
            Burst("burst_gold", "cosmetic_burst_gold", 70, new Color(1f, 0.80f, 0.30f), new Color(1f, 0.95f, 0.65f)),
            Burst("burst_clay", "cosmetic_burst_clay", 90, new Color(0.85f, 0.40f, 0.25f), new Color(0.95f, 0.65f, 0.40f)),
            Burst("burst_quetzal", "cosmetic_burst_quetzal", 120, new Color(0.20f, 0.85f, 0.75f), new Color(0.40f, 0.60f, 1f)),
        };

        AssetDatabase.CreateAsset(catalog, CatalogPath);
        AssetDatabase.SaveAssets();
        string created = $"Created {CatalogPath} with {catalog.items.Length} items. Clear 'Enable Cosmetics' to switch them off.";
        Debug.Log($"[CosmeticCatalogSetup] {created}");
        return created;
    }

    private static CosmeticItem Shift(string id, string nameKey, int price, Color color, bool keep = false) =>
        new CosmeticItem { id = id, kind = CosmeticKind.KukulkanShift, nameKey = nameKey, price = price, color = color, color2 = color, keepGameLook = keep };

    private static CosmeticItem Burst(string id, string nameKey, int price, Color a, Color b, bool keep = false) =>
        new CosmeticItem { id = id, kind = CosmeticKind.LandingBurst, nameKey = nameKey, price = price, color = a, color2 = b, keepGameLook = keep };
}
