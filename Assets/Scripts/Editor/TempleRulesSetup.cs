#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hooks the temple rules and powers up to their art and sound by file name.
///
/// Drop files into the folders below with the listed names (any image/audio extension), then
/// run TamalStacker ▸ Temples ▸ Wire Rule Art &amp; Audio (rules) or TamalStacker ▸ Powers ▸
/// Wire Power Art &amp; Audio (powers). Each run:
///  - fixes import settings (sprites as Sprite; tiled water as Repeat/FullRect; loops vs
///    one-shots get the right audio load type, mono);
///  - assigns every file it finds, and leaves fields whose file is missing untouched;
///  - reports what it wired and what is still missing.
///
/// Missing art is never an error: the game draws a code placeholder until the file exists.
/// </summary>
public static class TempleRulesSetup
{
    public const string RulesArtFolder = "Assets/Art/Rules";
    public const string RulesAudioFolder = "Assets/Art/Audio/Rules";
    private const string ArtAssetPath = "Assets/Resources/" + TempleRuleArt.ResourcePath + ".asset";
    private const string DialogTitle = "Temple Rules";

    private enum Kind { Sprite, TiledSprite, OneShot, Loop }

    // field on TempleRuleArt → (file name without extension, kind)
    private static readonly (string field, string file, Kind kind)[] RuleFiles =
    {
        ("ruleIntroSting",     "SFX_Rule_Intro",          Kind.OneShot),
        ("windLeaf",           "Rule_Wind_Leaf",          Kind.Sprite),
        ("windLoop",           "SFX_Wind_Loop",           Kind.Loop),
        ("windGustSound",      "SFX_Wind_Gust",           Kind.OneShot),
        ("rainStreak",         "Rule_Rain_Streak",        Kind.Sprite),
        ("rainLoop",           "SFX_Rain_Loop",           Kind.Loop),
        ("rainSlideSound",     "SFX_Rain_Slide",          Kind.OneShot),
        ("quakeHoldIcon",      "Rule_Quake_HoldIcon",     Kind.Sprite),
        ("quakeRumbleLoop",    "SFX_Quake_Rumble_Loop",   Kind.Loop),
        ("quakeJoltSound",     "SFX_Quake_Jolt",          Kind.OneShot),
        ("quakeBraceSound",    "SFX_Quake_Brace",         Kind.OneShot),
        ("cenoteWater",        "Rule_Cenote_Water",       Kind.TiledSprite),
        ("cenoteSurface",      "Rule_Cenote_Surface",     Kind.TiledSprite),
        ("cenoteLoop",         "SFX_Cenote_Loop",         Kind.Loop),
        ("cenoteWarningSound", "SFX_Cenote_Warning",      Kind.OneShot),
        ("cenoteFloodSound",   "SFX_Cenote_Flood",        Kind.OneShot),
        ("eclipseSun",         "Rule_Eclipse_Sun",        Kind.Sprite),
        ("eclipseSting",       "SFX_Eclipse_Sting",       Kind.OneShot),
        ("eclipseLoop",        "SFX_Eclipse_Loop",        Kind.Loop),
    };

    [MenuItem("TamalStacker/Temples/Wire Rule Art & Audio")]
    public static void WireRuleArt()
    {
        EnsureFolder(RulesArtFolder);
        EnsureFolder(RulesAudioFolder);

        var art = AssetDatabase.LoadAssetAtPath<TempleRuleArt>(ArtAssetPath);
        bool created = false;
        if (art == null)
        {
            EnsureFolder("Assets/Resources");
            art = ScriptableObject.CreateInstance<TempleRuleArt>();
            AssetDatabase.CreateAsset(art, ArtAssetPath);
            created = true;
        }

        var so = new SerializedObject(art);
        var wired = new List<string>();
        var missing = new List<string>();

        foreach (var (field, file, kind) in RuleFiles)
        {
            string folder = kind == Kind.OneShot || kind == Kind.Loop ? RulesAudioFolder : RulesArtFolder;
            Object asset = FindAndImport(folder, file, kind);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError("[TempleRulesSetup] TempleRuleArt has no field " + field);
                continue;
            }

            if (asset != null)
            {
                prop.objectReferenceValue = asset;
                wired.Add(file);
            }
            else if (prop.objectReferenceValue == null)
            {
                missing.Add(folder + "/" + file);
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(art);

        WireCapstones(wired, missing);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(DialogTitle,
            (created ? "Created " : "Updated ") + ArtAssetPath + "\n\n" +
            Report(wired, missing) +
            "\n\nMissing files keep their code placeholder (or stay silent).",
            "OK");
        Selection.activeObject = art;
    }

    /// <summary>
    /// Capstone_NN (NN = level number, two digits) in Art/Rules/Capstones → that temple's
    /// capstone silhouette. Temples without a file keep the gold-flash-only capstone.
    /// </summary>
    private static void WireCapstones(List<string> wired, List<string> missing)
    {
        string folder = RulesArtFolder + "/Capstones";
        EnsureFolder(folder);
        int without = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:LevelData"))
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
            if (level == null) continue;

            string file = "Capstone_" + level.levelNumber.ToString("00");
            Sprite sprite = FindSprite(folder, file);
            if (sprite != null)
            {
                level.capstoneSilhouette = sprite;
                EditorUtility.SetDirty(level);
                wired.Add(file);
            }
            else if (level.capstoneSilhouette == null)
            {
                without++;
            }
        }

        if (without > 0) missing.Add(folder + "/Capstone_NN - " + without + " temple(s) still flash-only");
    }

    [MenuItem("TamalStacker/Temples/Print Region Rules")]
    public static void PrintRegionRules()
    {
        var sb = new StringBuilder("[TempleRulesSetup] Temple rules:\n");
        var levels = new List<LevelData>();
        foreach (string guid in AssetDatabase.FindAssets("t:LevelData"))
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
            if (level != null) levels.Add(level);
        }
        levels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));

        foreach (LevelData l in levels)
        {
            sb.AppendLine($"  {l.levelNumber,2} {l.levelName,-22} {l.rule}{(l.secondRule != LevelRule.None ? " + " + l.secondRule : "")}" +
                          $"  swing {l.swingSpeedMultiplier}/{l.swingAmplitudeMultiplier}  capstone {(l.capstonePayoff ? (l.capstoneSilhouette != null ? "silhouette" : "flash") : "off")}");
        }
        Debug.Log(sb.ToString());
    }

    [MenuItem("TamalStacker/Temples/Debug/Show Rule Banners Again")]
    public static void DebugReplayIntros()
    {
        TempleRules.ResetSessionIntros();
        Debug.Log("[TempleRulesSetup] Rule banners will show again on the next attempt at each temple (play mode session).");
    }

    // ---- Shared with PowersSetup ----

    internal static string Report(List<string> wired, List<string> missing)
    {
        var sb = new StringBuilder();
        sb.Append("Wired ").Append(wired.Count).Append(wired.Count == 1 ? " file" : " files");
        if (wired.Count > 0) sb.Append(":\n  ").Append(string.Join("\n  ", wired));
        if (missing.Count > 0)
        {
            sb.Append("\n\nStill missing ").Append(missing.Count).Append(":\n  ").Append(string.Join("\n  ", missing));
        }
        return sb.ToString();
    }

    internal static Sprite FindSprite(string folder, string file, bool tiled = false) =>
        FindAndImport(folder, file, tiled ? Kind.TiledSprite : Kind.Sprite) as Sprite;

    internal static AudioClip FindClip(string folder, string file, bool loop = false) =>
        FindAndImport(folder, file, loop ? Kind.Loop : Kind.OneShot) as AudioClip;

    /// <summary>Finds <paramref name="file"/>.* in <paramref name="folder"/>, fixes its import settings, loads it.</summary>
    private static Object FindAndImport(string folder, string file, Kind kind)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return null;

        string path = null;
        foreach (string guid in AssetDatabase.FindAssets(file, new[] { folder }))
        {
            string candidate = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(candidate) == file && !AssetDatabase.IsValidFolder(candidate))
            {
                path = candidate;
                break;
            }
        }
        if (path == null) return null;

        bool sprite = kind == Kind.Sprite || kind == Kind.TiledSprite;
        if (sprite) ConfigureSprite(path, kind == Kind.TiledSprite);
        else ConfigureAudio(path, kind == Kind.Loop);

        return sprite ? (Object)AssetDatabase.LoadAssetAtPath<Sprite>(path) : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static void ConfigureSprite(string path, bool tiled)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;

        bool changed = false;
        if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; changed = true; }
        if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
        if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }

        if (tiled)
        {
            // Tiled draw mode needs a full-rect mesh and a repeating texture.
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(settings);
                changed = true;
            }
            if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; changed = true; }
        }

        if (changed) ti.SaveAndReimport();
    }

    private static void ConfigureAudio(string path, bool loop)
    {
        if (!(AssetImporter.GetAtPath(path) is AudioImporter ai)) return;

        AudioImporterSampleSettings s = ai.defaultSampleSettings;
        // Loops stream-ish from compressed memory; short one-shots decompress for zero-latency playback.
        AudioClipLoadType load = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        bool changed = false;
        if (s.loadType != load) { s.loadType = load; changed = true; }
        if (s.compressionFormat != AudioCompressionFormat.Vorbis) { s.compressionFormat = AudioCompressionFormat.Vorbis; changed = true; }
        if (!ai.forceToMono) { ai.forceToMono = true; changed = true; }

        if (changed)
        {
            ai.defaultSampleSettings = s;
            ai.SaveAndReimport();
        }
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
