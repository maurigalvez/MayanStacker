#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Moves the three sky themes out of the level-select screen and into the Jade Market.
///   • the theme pictures the level-select buttons used (Day, Sunset unlocked, Night unlocked) become the
///     market cards' pictures in the Cosmetic Catalog, so the market shows the same art
///   • the theme buttons, their "locked" info panel and their confirmation panel are removed from the
///     Main Menu Canvas prefab, and the menu no longer points at the old theme screen
///
/// Themes still unlock free from temples (7 and 15 cleared); the market lets jade buy them early, and
/// shows the free route on the card. Menu: TamalStacker ▸ UI ▸ Move Themes To Market. Safe to run again.
/// </summary>
public static class ThemesToMarketSetup
{
    private const string MainMenuPrefabPath = "Assets/Prefabs/Main Menu/Main Menu Canvas.prefab";
    private const string CatalogPath = "Assets/Resources/CosmeticCatalog.asset";

    [MenuItem("TamalStacker/UI/Move Themes To Market")]
    public static void Run()
    {
        EditorUtility.DisplayDialog("Themes", Move(), "OK");
    }

    /// <summary>Non-interactive twin for the Unity CLI.</summary>
    public static string RunBatch() => Move();

    private static string Move()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CatalogPath);
        if (catalog == null) return CatalogPath + " was not found. Run TamalStacker > Economy > Create Cosmetic Catalog first.";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath) == null) return MainMenuPrefabPath + " was not found.";

        var report = new StringBuilder();
        GameObject root = PrefabUtility.LoadPrefabContents(MainMenuPrefabPath);
        try
        {
            var ui = root.GetComponentInChildren<ThemeSelectionUI>(true);
            if (ui == null)
            {
                report.AppendLine("The level-select theme screen is already gone from the Main Menu Canvas.");
            }
            else
            {
                var so = new SerializedObject(ui);
                Sprite day = ImageSprite(so, "dayThemeButton");
                Sprite sunset = so.FindProperty("sunsetThemeUnlockedSprite").objectReferenceValue as Sprite;
                Sprite night = so.FindProperty("nightThemeUnlockedSprite").objectReferenceValue as Sprite;
                AssignIcons(catalog, day, sunset, night, report);

                // Everything the old screen owned, collected before anything is destroyed.
                var doomed = new System.Collections.Generic.List<GameObject>();
                foreach (string field in new[] { "dayThemeButton", "sunsetThemeButton", "nightThemeButton" })
                {
                    var button = so.FindProperty(field).objectReferenceValue as Component;
                    if (button != null) doomed.Add(button.gameObject);
                }
                foreach (string field in new[] { "unlockInfoPanel", "confirmationPanel" })
                {
                    var panel = so.FindProperty(field).objectReferenceValue as GameObject;
                    if (panel != null) doomed.Add(panel);
                }

                // The strip holding the buttons goes with them once nothing else lives in it.
                Transform strip = doomed.Count > 0 ? doomed[0].transform.parent : null;

                foreach (var menu in root.GetComponentsInChildren<MainMenuManager>(true))
                {
                    var menuSo = new SerializedObject(menu);
                    var prop = menuSo.FindProperty("themeSelectionUI");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = null;
                        menuSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                var host = ui.gameObject;
                Object.DestroyImmediate(ui);
                foreach (var go in doomed)
                {
                    if (go == null || go == root) continue;
                    report.AppendLine("Removed " + PathOf(go.transform, root.transform));
                    Object.DestroyImmediate(go);
                }
                if (strip != null && strip != root.transform && strip.childCount == 0 && strip.GetComponents<Component>().Length <= 3)
                {
                    report.AppendLine("Removed the empty strip " + PathOf(strip, root.transform));
                    Object.DestroyImmediate(strip.gameObject);
                }
                if (host != null && host.transform.childCount == 0 && host.GetComponents<Component>().Length <= 2)
                {
                    report.AppendLine("Removed the empty holder " + PathOf(host.transform, root.transform));
                    Object.DestroyImmediate(host);
                }
                PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ThemesToMarketSetup]\n" + report);
        return report.ToString().TrimEnd();
    }

    private static Sprite ImageSprite(SerializedObject so, string field)
    {
        var button = so.FindProperty(field).objectReferenceValue as Button;
        var image = button != null ? button.GetComponent<Image>() : null;
        return image != null ? image.sprite : null;
    }

    private static void AssignIcons(CosmeticCatalog catalog, Sprite day, Sprite sunset, Sprite night, StringBuilder report)
    {
        bool changed = false;
        foreach (var item in catalog.items)
        {
            if (item == null || item.kind != CosmeticKind.Theme) continue;
            Sprite icon = item.theme == GameTheme.Sunset ? sunset : item.theme == GameTheme.Night ? night : day;
            if (icon == null || item.icon == icon) continue;
            item.icon = icon;
            changed = true;
            report.AppendLine("Market picture for " + item.id + ": " + icon.name);
        }
        if (!changed) return;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
    }

    private static string PathOf(Transform t, Transform stop)
    {
        string path = t.name;
        while (t.parent != null && t.parent != stop)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
#endif
