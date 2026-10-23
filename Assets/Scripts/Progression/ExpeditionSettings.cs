using System;
using UnityEngine;

/// <summary>
/// Tuning for the Weekly Expedition: five stages walked in order across the UTC week, each a
/// small goal on the runs the player plays anyway. Finishing a stage pays jade; finishing all five
/// pays a finale and earns the week's badge.
///
/// An asset in Resources; if it is missing, or <see cref="enableExpedition"/> is cleared, nothing
/// is tracked and the game plays exactly as before.
/// </summary>
[CreateAssetMenu(fileName = "ExpeditionSettings", menuName = "TamalStacker/Expedition Settings", order = 13)]
public class ExpeditionSettings : ScriptableObject
{
    public const string ResourcePath = "ExpeditionSettings";

    private static ExpeditionSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or the expedition is off.</summary>
    public static ExpeditionSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<ExpeditionSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableExpedition && cached.stages.Length > 0 ? cached : null;
        }
    }

    [Serializable]
    public class Stage
    {
        [Tooltip("Stable id. The description is the loc key expedition_<id>, with the target as {0}.")]
        public string id;
        public MissionSettings.Kind kind;
        public MissionSettings.ModeFilter mode;
        [Min(1)] public int target = 1;
        [Min(0)] public int reward = 15;
    }

    [Header("Master switch")]
    public bool enableExpedition = true;

    [Header("Rules")]
    [Tooltip("A run that falls before this many stones counts for nothing, so instant restarts can't tick stages.")]
    [Min(0)] public int minStonesToCount = 3;

    [Tooltip("Jade for finishing every stage of the week.")]
    [Min(0)] public int finaleReward = 50;

    [Header("Stages (walked in order; the same five every week)")]
    public Stage[] stages = new Stage[0];
}
