using UnityEditor;
using UnityEngine;

/// <summary>
/// One place to flip every "Spins" system on/off for A/B tests and to poke their editor-side state.
/// Switches edit the Resources settings assets (undoable, saved with the project); the buttons act
/// on Editor PlayerPrefs only. Nothing here submits or queues a leaderboard score.
/// </summary>
public class SpinsDebugWindow : EditorWindow
{
    // (label, asset path under Assets/Resources, bool field names)
    private static readonly (string label, string path, string[] fields)[] Switches =
    {
        ("Tempo", "TempoSettings", new[] { "enableTempo", "enableFastRetry" }),
        ("Swing moods", "SwingMoodSettings", new[] { "enableMoods", "enablePacing", "enableInTemples", "enableInDaily", "enableInInfinite" }),
        ("Perfect ladder", "PerfectLadderSettings", new[] { "enableLadder" }),
        ("Sun Surge", "SunSurgeSettings", new[] { "enableSurge" }),
        ("Terraces", "TerraceSettings", new[] { "enableTerraces" }),
        ("Clutch moments", "ClutchMomentSettings", new[] { "enableMoments" }),
        ("Jade", "JadeSettings", new[] { "enableJade" }),
        ("Run reveal", "RunRevealSettings", new[] { "enableReveal", "enableDoubleJade" }),
        ("Second Chance", "SecondChanceSettings", new[] { "enableSecondChance" }),
        ("Missions", "MissionSettings", new[] { "enableMissions", "enableReroll" }),
        ("Login chest", "LoginChestSettings", new[] { "enableLoginChest" }),
        ("Path of Kukulkan", "PathSettings", new[] { "enablePath" }),
        ("Share", "ShareSettings", new[] { "enableShare" }),
        ("Weekly Expedition", "ExpeditionSettings", new[] { "enableExpedition" }),
    };

    private Vector2 scroll;

    [MenuItem("TamalStacker/Spins/Debug/Spins Switches...")]
    public static void Open() => GetWindow<SpinsDebugWindow>("Spins Switches");

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Switches edit the settings assets in Resources. Untick to roll a system back.", MessageType.Info);

        foreach (var (label, path, fields) in Switches)
        {
            var asset = Resources.Load<ScriptableObject>(path);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (asset == null)
            {
                EditorGUILayout.LabelField("  missing Resources/" + path + " (run its setup menu)");
                continue;
            }
            var so = new SerializedObject(asset);
            so.Update();
            EditorGUI.BeginChangeCheck();
            foreach (string field in fields)
            {
                var prop = so.FindProperty(field);
                if (prop != null) EditorGUILayout.PropertyField(prop);
            }
            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editor-only state", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+100 jade")) JadeWallet.Earn("debug", 100);
        if (GUILayout.Button("Reset lessons")) SpinLessons.ResetAll();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset missions")) DailyMissions.ResetAll();
        if (GUILayout.Button("Reset expedition")) WeeklyExpedition.ResetAll();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Balance: " + JadeWallet.Balance);
        EditorGUILayout.EndScrollView();
    }
}
