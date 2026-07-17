#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

/// <summary>
/// One-click setup for Chinese / Japanese (CJK) text rendering.
///
/// The game's localization now ships zh-Hans, zh-Hant and ja strings, but the
/// project's display fonts (Evil Empire, Stoneburg, Mayan, Aztec, LiberationSans)
/// only contain Latin glyphs — so CJK characters render as blank boxes (□).
///
/// This tool:
///   1. Scans <see cref="SourceFontFolder"/> for any .ttf / .otf font files.
///   2. Builds a DYNAMIC TMP_FontAsset for each (glyphs rasterized on demand, so
///      the atlas stays tiny instead of baking 20,000+ CJK glyphs up front).
///   3. Registers those assets in TMP Settings' GLOBAL fallback list, so every
///      TMP label in the game (whatever its primary font) can resolve CJK glyphs.
///
/// Usage:
///   - Download a CJK font that covers Simplified + Traditional + Japanese, e.g.
///     Noto Sans CJK (SC/TC/JP) or Source Han Sans. A single "CJK" variant covers
///     all three of our locales. (Brazilian Portuguese is Latin — already covered.)
///   - Drop the .ttf/.otf into  Assets/Art/Fonts/CJK/
///   - Menu:  TamalStacker ▸ Localization ▸ Setup CJK Font Fallback
///
/// Idempotent: re-running reuses existing generated assets and won't duplicate
/// fallback entries.
///
/// Menu: TamalStacker ▸ Localization ▸ …
/// </summary>
public static class CJKFontFallbackSetup
{
    public const string SourceFontFolder = "Assets/Art/Fonts/CJK";
    public const string GeneratedFolder = "Assets/Art/Fonts/CJK/Generated";

    // Atlas / rasterization settings for the dynamic fallback font.
    private const int SamplingPointSize = 90;
    private const int AtlasPadding = 9;
    private const int AtlasWidth = 1024;
    private const int AtlasHeight = 1024;

    [MenuItem("TamalStacker/Localization/Setup CJK Font Fallback")]
    public static void SetupFallback()
    {
        if (!Directory.Exists(SourceFontFolder))
        {
            Directory.CreateDirectory(SourceFontFolder);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "CJK Font Fallback",
                "Created folder:\n\n" + SourceFontFolder +
                "\n\nDrop a CJK font (.ttf/.otf) that covers Simplified Chinese, " +
                "Traditional Chinese and Japanese into that folder — for example " +
                "Noto Sans CJK (SC/TC/JP) or Source Han Sans — then run this menu " +
                "item again.",
                "OK");
            return;
        }

        var sourceFonts = FindSourceFonts();
        if (sourceFonts.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "CJK Font Fallback",
                "No .ttf/.otf font files found in:\n\n" + SourceFontFolder +
                "\n\nDownload a CJK font (Noto Sans CJK or Source Han Sans), drop it " +
                "into that folder, then run this menu item again.",
                "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            AssetDatabase.CreateFolder(SourceFontFolder, "Generated");

        var fallbackAssets = new List<TMP_FontAsset>();
        foreach (var font in sourceFonts)
        {
            var fontAsset = EnsureDynamicFontAsset(font);
            if (fontAsset != null)
                fallbackAssets.Add(fontAsset);
        }

        int added = RegisterGlobalFallbacks(fallbackAssets);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "CJK Font Fallback",
            $"Done.\n\nGenerated/verified {fallbackAssets.Count} dynamic CJK font " +
            $"asset(s) and added {added} new entr(y/ies) to the TMP Settings global " +
            "fallback list.\n\nEnter Play mode and switch to a Chinese or Japanese " +
            "language to confirm the glyphs render.",
            "OK");
    }

    public static List<Font> FindSourceFonts()
    {
        var result = new List<Font>();
        string[] guids = AssetDatabase.FindAssets("t:Font", new[] { SourceFontFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Skip anything under the Generated subfolder.
            if (path.StartsWith(GeneratedFolder)) continue;
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font != null) result.Add(font);
        }
        return result;
    }

    public static TMP_FontAsset EnsureDynamicFontAsset(Font font)
    {
        if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            AssetDatabase.CreateFolder(SourceFontFolder, "Generated");

        string assetPath = $"{GeneratedFolder}/{font.name} SDF.asset";

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
            return existing;

        // Dynamic atlas: glyphs are rasterized from the source font at runtime,
        // so we don't bake the entire CJK range into the texture.
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            font,
            SamplingPointSize,
            AtlasPadding,
            GlyphRenderMode.SDFAA,
            AtlasWidth,
            AtlasHeight,
            AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError($"CJKFontFallbackSetup: Failed to create TMP font asset for '{font.name}'.");
            return null;
        }

        fontAsset.name = font.name + " SDF";
        AssetDatabase.CreateAsset(fontAsset, assetPath);

        // The atlas texture and material are created in-memory; persist them as
        // sub-assets of the font asset so the reference survives reimport.
        if (fontAsset.atlasTextures != null)
        {
            foreach (var tex in fontAsset.atlasTextures)
            {
                if (tex != null && !AssetDatabase.Contains(tex))
                {
                    tex.name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
                }
            }
        }
        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);

        Debug.Log($"CJKFontFallbackSetup: Created dynamic TMP font asset at '{assetPath}'.");
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
    }

    private static int RegisterGlobalFallbacks(List<TMP_FontAsset> fallbacks)
    {
        var settings = TMP_Settings.instance;
        if (settings == null)
        {
            Debug.LogError("CJKFontFallbackSetup: TMP_Settings not found. Import TMP Essentials first.");
            return 0;
        }

        var so = new SerializedObject(settings);
        var listProp = so.FindProperty("m_fallbackFontAssets");
        if (listProp == null)
        {
            Debug.LogError("CJKFontFallbackSetup: Could not find m_fallbackFontAssets on TMP_Settings.");
            return 0;
        }

        // Collect existing references to avoid duplicates.
        var existing = new HashSet<Object>();
        for (int i = 0; i < listProp.arraySize; i++)
            existing.Add(listProp.GetArrayElementAtIndex(i).objectReferenceValue);

        int added = 0;
        foreach (var fa in fallbacks)
        {
            if (fa == null || existing.Contains(fa)) continue;
            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = fa;
            existing.Add(fa);
            added++;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
        return added;
    }
}
#endif
