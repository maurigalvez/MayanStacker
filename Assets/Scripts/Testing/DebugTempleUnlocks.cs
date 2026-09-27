#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Editor-only test override that opens chosen temples without touching real progress, so a
/// rule (Cabracán, Rain, Eclipse, Wind, Cenote) can be checked straight away. Stored in
/// EditorPrefs rather than PlayerPrefs, so it never reaches the cloud save or a build.
/// A temple opened this way doesn't save stars when it's beaten (see LevelManager).
/// Edited from TamalStacker ▸ Temples ▸ Debug ▸ Temple Unlocks.
/// </summary>
public static class DebugTempleUnlocks
{
    private const string Key = "TamalStacker.DebugTempleUnlocks";

    private static HashSet<int> cache;

    private static HashSet<int> Set
    {
        get
        {
            if (cache != null) return cache;
            cache = new HashSet<int>();
            foreach (string part in EditorPrefs.GetString(Key, "").Split(','))
            {
                if (int.TryParse(part, out int n)) cache.Add(n);
            }
            return cache;
        }
    }

    public static bool IsForced(int levelNumber) => Set.Contains(levelNumber);

    public static int Count => Set.Count;

    public static void SetForced(int levelNumber, bool forced)
    {
        bool changed = forced ? Set.Add(levelNumber) : Set.Remove(levelNumber);
        if (changed) Save();
    }

    public static void Clear()
    {
        Set.Clear();
        Save();
    }

    private static void Save()
    {
        var numbers = new List<int>(Set);
        numbers.Sort();
        EditorPrefs.SetString(Key, string.Join(",", numbers));
    }
}
#endif
