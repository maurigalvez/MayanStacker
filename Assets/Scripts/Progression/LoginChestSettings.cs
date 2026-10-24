using UnityEngine;

/// <summary>
/// Tuning for the daily login chest: one jade chest a day whose size climbs over a cycle of
/// consecutive days. A single missed day is forgiven (the cycle holds), a longer gap restarts it.
///
/// An asset in Resources; if it is missing, or <see cref="enableLoginChest"/> is cleared, no chest
/// is offered and the game plays exactly as before.
/// </summary>
[CreateAssetMenu(fileName = "LoginChestSettings", menuName = "TamalStacker/Login Chest Settings", order = 13)]
public class LoginChestSettings : ScriptableObject
{
    public const string ResourcePath = "LoginChestSettings";

    private static LoginChestSettings cached;
    private static bool loaded;

    /// <summary>The active settings, or null when the asset is missing or the chest is off.</summary>
    public static LoginChestSettings Active
    {
        get
        {
            if (!loaded)
            {
                cached = Resources.Load<LoginChestSettings>(ResourcePath);
                loaded = true;
            }
            return cached != null && cached.enableLoginChest ? cached : null;
        }
    }

    [Tooltip("Master switch. Off: no chest, no menu chip.")]
    public bool enableLoginChest = true;

    [Tooltip("Jade for each day of the cycle, day 1 first. After the last entry the cycle starts over.")]
    public int[] cycleRewards = { 10, 15, 20, 25, 30, 40, 75 };

    [Tooltip("Extra jade per day of the Ritual of the Sun streak, added to the chest.")]
    public int bonusPerRitualStreakDay = 2;

    [Tooltip("Cap on the Ritual streak bonus.")]
    public int ritualBonusCap = 10;

    [Tooltip("Days between forgiven misses. One missed day inside this window keeps the cycle going.")]
    public int forgiveCooldownDays = 7;

    [Tooltip("Welcome-back bonus for players who were here before the release: chest jade is multiplied by this. 1 = off.")]
    public int returningBonusMultiplier = 2;

    [Tooltip("How many days the welcome-back bonus runs, counted from the day the release card appears.")]
    public int returningBonusDays = 7;

    public int RewardForDay(int cycleDay)
    {
        if (cycleRewards == null || cycleRewards.Length == 0) return 0;
        int i = Mathf.Max(0, cycleDay - 1) % cycleRewards.Length;
        return Mathf.Max(0, cycleRewards[i]);
    }
}
