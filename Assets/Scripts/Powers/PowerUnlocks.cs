using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which powers the player has earned, and which they have been introduced to.
///
/// PlayerPrefs-backed and synced through <see cref="PlayerProgressData.unlockedPowers"/>
/// the same way <see cref="InfiniteBest"/> is: a power is unlocked if either side says so,
/// so a sync can never take one away.
///
/// A power whose unlock temple is already completed counts as unlocked even without the
/// flag, so players who beat Dzibilchaltún before powers existed get Jaguar Slam without
/// replaying it.
/// </summary>
public static class PowerUnlocks
{
    private const string UnlockKeyPrefix = "Power_Unlocked_";
    private const string IntroKeyPrefix = "Power_IntroSeen_";
    private const string EquippedKey = "Power_Equipped";

    private static readonly PowerId[] AllPowers = (PowerId[])Enum.GetValues(typeof(PowerId));

    /// <summary>
    /// The power the meter fires: the player's pick if it is unlocked, otherwise the first
    /// unlocked power (so a stale or cloud-restored pick can never equip a locked power).
    /// </summary>
    public static PowerId Equipped
    {
        get
        {
            int stored = PlayerPrefs.GetInt(EquippedKey, -1);
            if (Enum.IsDefined(typeof(PowerId), stored) && IsUnlocked((PowerId)stored)) return (PowerId)stored;

            foreach (PowerId id in AllPowers)
            {
                if (IsUnlocked(id)) return id;
            }
            return PowerId.JaguarSlam;
        }
    }

    /// <summary>Picks the power the meter fires. Ignored for a locked power.</summary>
    public static void Equip(PowerId id)
    {
        if (!IsUnlocked(id)) return;
        PlayerPrefs.SetInt(EquippedKey, (int)id);
        PlayerPrefs.Save();
    }

    /// <summary>Every unlocked power, in enum (unlock) order. Fills <paramref name="into"/>.</summary>
    public static void GetUnlocked(List<PowerId> into)
    {
        into.Clear();
        foreach (PowerId id in AllPowers)
        {
            if (IsUnlocked(id)) into.Add(id);
        }
    }

    public static bool IsUnlocked(PowerId id)
    {
        if (PlayerPrefs.GetInt(UnlockKeyPrefix + id, 0) == 1) return true;

        // Backfill from level progress (LevelManager's own key), for temples beaten before
        // powers existed or restored from a cloud save.
        int level = PowerSettings.Current.Get(id).unlockedByLevel;
        if (level <= 0) return true;
        if (PlayerPrefs.GetInt($"Level_{level}_Stars", 0) > 0)
        {
            SetUnlocked(id);
            return true;
        }

        return false;
    }

    /// <summary>True when the player has any power (the equipped one falls back to it).</summary>
    public static bool HasEquippedPower => IsUnlocked(Equipped);

    /// <summary>Unlocks <paramref name="id"/>. Returns true only when it wasn't already.</summary>
    public static bool Unlock(PowerId id)
    {
        if (PlayerPrefs.GetInt(UnlockKeyPrefix + id, 0) == 1) return false;
        SetUnlocked(id);
        return true;
    }

    private static void SetUnlocked(PowerId id)
    {
        PlayerPrefs.SetInt(UnlockKeyPrefix + id, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Returns every power unlocked by completing <paramref name="levelNumber"/>.</summary>
    public static List<PowerId> PowersUnlockedBy(int levelNumber)
    {
        var result = new List<PowerId>();
        foreach (PowerId id in Enum.GetValues(typeof(PowerId)))
        {
            if (PowerSettings.Current.Get(id).unlockedByLevel == levelNumber) result.Add(id);
        }
        return result;
    }

    // ---- First-time intro ----

    public static bool HasSeenIntro(PowerId id) => PlayerPrefs.GetInt(IntroKeyPrefix + id, 0) == 1;

    public static void MarkIntroSeen(PowerId id)
    {
        if (HasSeenIntro(id)) return;
        PlayerPrefs.SetInt(IntroKeyPrefix + id, 1);
        PlayerPrefs.Save();
    }

    // ---- Cloud sync ----

    /// <summary>Copies local unlocks into outgoing cloud data.</summary>
    public static void WriteTo(PlayerProgressData data)
    {
        if (data == null) return;
        data.unlockedPowers = new List<string>();
        foreach (PowerId id in Enum.GetValues(typeof(PowerId)))
        {
            if (PlayerPrefs.GetInt(UnlockKeyPrefix + id, 0) == 1) data.unlockedPowers.Add(id.ToString());
        }
    }

    /// <summary>Adopts every unlock the cloud knows about. Never removes a local one.</summary>
    public static void MergeFromCloud(PlayerProgressData data)
    {
        if (data?.unlockedPowers == null) return;
        foreach (string name in data.unlockedPowers)
        {
            if (Enum.TryParse(name, out PowerId id)) Unlock(id);
        }
    }

    /// <summary>Forgets every unlock and intro. Used by data resets.</summary>
    public static void ResetAll()
    {
        foreach (PowerId id in Enum.GetValues(typeof(PowerId)))
        {
            PlayerPrefs.DeleteKey(UnlockKeyPrefix + id);
            PlayerPrefs.DeleteKey(IntroKeyPrefix + id);
        }
        PlayerPrefs.DeleteKey(EquippedKey);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Editor/debug: force a power's state. Locking only clears the flag — a completed unlock
    /// temple will grant it straight back, so reset level progress too to test the unlock.
    /// </summary>
    public static void DebugSetUnlocked(PowerId id, bool unlocked)
    {
        if (unlocked) SetUnlocked(id);
        else PlayerPrefs.DeleteKey(UnlockKeyPrefix + id);
        PlayerPrefs.Save();
    }
}
