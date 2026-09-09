#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;

/// <summary>
/// Keeps the PLAY_APP_UPDATE scripting define in step with whether Google's In-App Update
/// plugin is actually in the project.
///
/// <see cref="AppUpdatePrompt"/> has to reference types from Google.Play.AppUpdate, which do
/// not exist until that plugin is imported. Without a guard the project would not compile at
/// all until someone remembered to import it, and would stop compiling again the moment it
/// was removed. So the feature is written behind PLAY_APP_UPDATE and this class owns that
/// symbol: it looks for the plugin's AppUpdateManager type on every domain reload and adds or
/// removes the define to match. Import the plugin and the feature switches itself on; delete
/// it and everything reverts to a harmless stub.
///
/// Two places have to be kept in step, because this project builds through Build Profiles.
///
///   • Player Settings, for anyone building from the classic Build Settings window or a
///     profile that does not override them.
///   • Every Android Build Profile's own Scripting Defines list. Android_Release overrides
///     Player Settings wholesale — its snapshot pins scriptingDefineSymbols to
///     APP_UI_EDITOR_ONLY — so a symbol written only to Player Settings would be dropped from
///     the release build and the update prompt would silently compile out of the very build
///     that ships. Profile defines are additive to whatever Player Settings resolve to and
///     are stored per profile, which makes them the reliable half of this pair.
///
/// Profiles are edited through SerializedObject rather than the BuildProfile properties so
/// this keeps working regardless of which members Unity exposes publicly in a given version.
///
/// Nothing is written unless it actually needs to change: writing the active profile's
/// defines triggers a recompile, which would run this again, forever.
/// </summary>
[InitializeOnLoad]
public static class PlayAppUpdateDefine
{
    private const string Symbol = "PLAY_APP_UPDATE";
    private const string PluginTypeName = "Google.Play.AppUpdate.AppUpdateManager";

    static PlayAppUpdateDefine()
    {
        // Deferred: during the static constructor the assembly list can still be settling.
        EditorApplication.delayCall += Sync;
    }

    /// <summary>Re-checks and rewrites the define everywhere. Safe to call at any time.</summary>
    [MenuItem("TamalStacker/Retention/Refresh In-App Update Support")]
    public static void Sync()
    {
        bool pluginPresent = IsPluginPresent();

        var changed = new List<string>();
        SyncPlayerSettings(pluginPresent, changed);
        SyncBuildProfiles(pluginPresent, changed);

        if (changed.Count == 0) return;

        AssetDatabase.SaveAssets();

        Debug.Log(pluginPresent
            ? $"[PlayAppUpdateDefine] In-App Update plugin found — added {Symbol} to: {string.Join(", ", changed)}. The update prompt is now live."
            : $"[PlayAppUpdateDefine] In-App Update plugin missing — removed {Symbol} from: {string.Join(", ", changed)}. The update prompt is disabled.");
    }

    /// <summary>
    /// The global Android define set. Note that while a profile with Player Settings
    /// overrides is active, this API is wired to that profile's copy rather than the global
    /// one — which is exactly why the profile pass below exists as well.
    /// </summary>
    private static void SyncPlayerSettings(bool wanted, List<string> changed)
    {
        var target = NamedBuildTarget.Android;
        PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);

        bool present = Array.IndexOf(defines, Symbol) >= 0;
        if (present == wanted) return;

        var updated = new List<string>(defines);
        if (wanted) updated.Add(Symbol);
        else updated.Remove(Symbol);

        PlayerSettings.SetScriptingDefineSymbols(target, updated.ToArray());
        changed.Add("Player Settings");
    }

    /// <summary>
    /// Every Android build profile in the project, whether or not it is the active one — a
    /// profile that is wrong only at build time is the worst kind of wrong.
    /// </summary>
    private static void SyncBuildProfiles(bool wanted, List<string> changed)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
            if (profile == null) continue;

            var so = new SerializedObject(profile);

            var targetProp = so.FindProperty("m_BuildTarget");
            if (targetProp == null || targetProp.intValue != (int)BuildTarget.Android) continue;

            var hasProp = so.FindProperty("m_HasScriptingDefines");
            var listProp = so.FindProperty("m_ScriptingDefines");
            if (hasProp == null || listProp == null) continue;

            int index = IndexOf(listProp, Symbol);
            bool present = index >= 0;
            if (present == wanted) continue;

            if (wanted)
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).stringValue = Symbol;
                hasProp.boolValue = true;
            }
            else
            {
                listProp.DeleteArrayElementAtIndex(index);
                // Leave the profile as we found it when we were the only entry.
                if (listProp.arraySize == 0) hasProp.boolValue = false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            changed.Add(profile.name);
        }
    }

    private static int IndexOf(SerializedProperty arrayProp, string value)
    {
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            if (arrayProp.GetArrayElementAtIndex(i).stringValue == value) return i;
        }
        return -1;
    }

    private static bool IsPluginPresent()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // A dynamic or unloadable assembly can throw on GetType; none of those are ours.
            try
            {
                if (assembly.GetType(PluginTypeName, throwOnError: false) != null) return true;
            }
            catch (Exception)
            {
                // Ignore and keep looking.
            }
        }

        return false;
    }
}
#endif
