#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Per-language font wiring for the CJK locales.
///
/// Unlike the single global fallback (see <see cref="CJKFontFallbackSetup"/>),
/// this assigns a script-correct font per locale so Simplified, Traditional and
/// Japanese each render with the right glyph shapes:
///
///   1. Setup Per-Language Fonts — generates dynamic TMP font assets from the
///      region fonts in Assets/Art/Fonts/CJK/, then fills a
///      Resources/LocaleFontSet.asset by matching filename region tokens
///      (SC/CN → Simplified, TC/HK/TW → Traditional, JP → Japanese).
///   2. Attach Font Switchers ▸ … — adds a <see cref="LocaleFontSwitcher"/> to
///      every TMP label in open scenes and/or project prefabs.
///
/// Menu: TamalStacker ▸ Localization ▸ …
/// </summary>
public static class LocaleFontSetup
{
    private const string LocaleFontSetPath = "Assets/Resources/LocaleFontSet.asset";

    // Prefab folders that never contain game UI — skipped by the bulk attach.
    private static readonly string[] SkipFolders =
    {
        "Assets/PlayFabSdk", "Assets/TextMesh Pro", "Assets/GoogleMobileAds",
        "Assets/PlayFabEditorExtensions",
    };

    // Weight tokens ranked to match the chunky Latin display UI (heavier first).
    private static readonly string[] WeightPreference =
    {
        "BOLD", "HEAVY", "MEDIUM", "SEMIBOLD", "NORMAL", "REGULAR", "LIGHT", "EXTRALIGHT",
    };

    private enum Region { None, Simplified, Traditional, Japanese }

    // ---------------------------------------------------------------- Fonts

    [MenuItem("TamalStacker/Localization/Setup Per-Language Fonts")]
    public static void SetupPerLanguageFonts()
    {
        var sourceFonts = CJKFontFallbackSetup.FindSourceFonts();
        if (sourceFonts.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Per-Language Fonts",
                "No fonts found in " + CJKFontFallbackSetup.SourceFontFolder +
                ".\n\nDrop region fonts there, for example:\n" +
                "  • Source Han Sans SC (or Noto Sans SC) — Simplified\n" +
                "  • Source Han Sans TC/HK (or Noto Sans TC) — Traditional\n" +
                "  • Source Han Sans JP (or Noto Sans JP) — Japanese\n\n" +
                "One weight each is enough. Then run this again.",
                "OK");
            return;
        }

        // Pick the best-weight source font for each region.
        var best = new Dictionary<Region, (Font font, int rank)>();
        foreach (var font in sourceFonts)
        {
            Region region = Classify(font.name);
            if (region == Region.None) continue;

            int rank = WeightRank(font.name);
            if (!best.TryGetValue(region, out var cur) || rank < cur.rank)
                best[region] = (font, rank);
        }

        var set = LoadOrCreateFontSet();
        set.simplifiedChinese  = EnsureAssetFor(best, Region.Simplified);
        set.traditionalChinese = EnsureAssetFor(best, Region.Traditional);
        set.japanese           = EnsureAssetFor(best, Region.Japanese);
        EditorUtility.SetDirty(set);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string report =
            "LocaleFontSet updated:\n\n" +
            "  Simplified (zh-Hans):  " + Name(set.simplifiedChinese) + "\n" +
            "  Traditional (zh-Hant): " + Name(set.traditionalChinese) + "\n" +
            "  Japanese (ja):         " + Name(set.japanese) + "\n\n";

        var missing = new List<string>();
        if (set.simplifiedChinese == null)  missing.Add("Simplified (SC/CN)");
        if (set.traditionalChinese == null) missing.Add("Traditional (TC/HK/TW)");
        if (set.japanese == null)           missing.Add("Japanese (JP)");
        if (missing.Count > 0)
            report += "Still missing a font for: " + string.Join(", ", missing) +
                      ".\nAdd that region's font to the CJK folder and re-run.\n\n";

        report += "Next: TamalStacker ▸ Localization ▸ Attach Font Switchers ▸ …";
        EditorUtility.DisplayDialog("Per-Language Fonts", report, "OK");
    }

    private static TMP_FontAsset EnsureAssetFor(Dictionary<Region, (Font font, int rank)> best, Region region)
    {
        if (!best.TryGetValue(region, out var pick)) return null;
        return CJKFontFallbackSetup.EnsureDynamicFontAsset(pick.font);
    }

    private static Region Classify(string fontName)
    {
        string n = fontName.ToUpperInvariant();
        if (Contains(n, "SC") || Contains(n, "CN")) return Region.Simplified;
        if (Contains(n, "TC") || Contains(n, "HK") || Contains(n, "TW")) return Region.Traditional;
        if (Contains(n, "JP") || Contains(n, "JA")) return Region.Japanese;
        return Region.None;
    }

    // Match a region token bounded by non-letters so "SC" doesn't hit "Escape" etc.
    private static bool Contains(string upperName, string token)
    {
        int idx = 0;
        while ((idx = upperName.IndexOf(token, idx, System.StringComparison.Ordinal)) >= 0)
        {
            bool leftOk = idx == 0 || !char.IsLetter(upperName[idx - 1]);
            int end = idx + token.Length;
            bool rightOk = end >= upperName.Length || !char.IsLetter(upperName[end]);
            if (leftOk && rightOk) return true;
            idx = end;
        }
        return false;
    }

    private static int WeightRank(string fontName)
    {
        string n = fontName.ToUpperInvariant();
        for (int i = 0; i < WeightPreference.Length; i++)
            if (n.Contains(WeightPreference[i])) return i;
        return WeightPreference.Length; // unknown weight sorts last
    }

    private static LocaleFontSet LoadOrCreateFontSet()
    {
        var set = AssetDatabase.LoadAssetAtPath<LocaleFontSet>(LocaleFontSetPath);
        if (set != null) return set;

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        set = ScriptableObject.CreateInstance<LocaleFontSet>();
        AssetDatabase.CreateAsset(set, LocaleFontSetPath);
        return set;
    }

    private static string Name(Object o) => o != null ? o.name : "— (none)";

    // ------------------------------------------------------------- Attach

    [MenuItem("TamalStacker/Localization/Attach Font Switchers/To Open Scenes")]
    public static void AttachToOpenScenes()
    {
        int added = 0, scanned = 0;
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    scanned++;
                    if (t.GetComponent<LocaleFontSwitcher>() == null)
                    {
                        Undo.AddComponent<LocaleFontSwitcher>(t.gameObject);
                        added++;
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        EditorUtility.DisplayDialog(
            "Attach Font Switchers",
            $"Scanned {scanned} TMP label(s) in open scene(s); added {added} " +
            "LocaleFontSwitcher component(s).\n\nSave the scene(s) to persist.",
            "OK");
    }

    [MenuItem("TamalStacker/Localization/Attach Font Switchers/To Project Prefabs")]
    public static void AttachToProjectPrefabs()
    {
        if (!EditorUtility.DisplayDialog(
                "Attach Font Switchers",
                "This will add a LocaleFontSwitcher to every TMP label in your " +
                "project's prefabs and save them. Recommended: commit/back up first.\n\n" +
                "Proceed?",
                "Attach", "Cancel"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int prefabsChanged = 0, added = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (ShouldSkip(path)) continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Attach Font Switchers", path, (float)i / guids.Length))
                    break;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int localAdded = 0;
                    foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (t.GetComponent<LocaleFontSwitcher>() == null)
                        {
                            t.gameObject.AddComponent<LocaleFontSwitcher>();
                            localAdded++;
                        }
                    }
                    if (localAdded > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabsChanged++;
                        added += localAdded;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "Attach Font Switchers",
            $"Added {added} LocaleFontSwitcher component(s) across {prefabsChanged} prefab(s).",
            "OK");
    }

    private static bool ShouldSkip(string path)
    {
        foreach (var skip in SkipFolders)
            if (path.StartsWith(skip)) return true;
        return false;
    }
}
#endif
