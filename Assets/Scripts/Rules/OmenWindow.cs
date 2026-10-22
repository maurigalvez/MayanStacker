/// <summary>
/// Which temple rules are mid-omen right now: an eclipse in totality, a quake shaking, a fresh
/// gust, the cenote closing on the top stone. In the Daily a Perfect landed while any omen is
/// open is an "omen defied" and pays <see cref="GameManager"/>'s omen bonus, so the ritual's
/// events read as chances rather than only hazards.
///
/// Each rule opens and closes its own bit; <see cref="TempleRuleBehaviour"/> closes it again at
/// run start and end, so a rule that ends mid-omen can't leave the window stuck open.
/// </summary>
public static class OmenWindow
{
    private static int openMask;

    /// <summary>True while at least one omen is on.</summary>
    public static bool IsOpen => openMask != 0;

    public static void Set(LevelRule rule, bool open)
    {
        int bit = 1 << (int)rule;
        if (open) openMask |= bit;
        else openMask &= ~bit;
    }

    public static bool IsRuleOpen(LevelRule rule) => (openMask & (1 << (int)rule)) != 0;

    public static void Clear() => openMask = 0;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Clear();
}
