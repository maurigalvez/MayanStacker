/// <summary>
/// The power a Daily ritual hands out, the same for every player whether they have unlocked
/// it or not (the Daily is a fairness contract; it doubles as a trial of powers a player
/// hasn't reached yet).
///
/// Two shapes: a fixed power for the whole ritual, or Gift of the Gods — a random power on
/// every full meter. The gifts come from their own day-seeded hash rather than
/// <see cref="RunRandom"/>, so using a power never shifts the ritual's block sequence and the
/// Nth gift is the same for everyone that day.
///
/// Set by <see cref="DailyChallengeManager.ApplyModifier"/>, read by <see cref="PowerSystem"/>
/// and <see cref="GameManager.StreakShiftActive"/>. Only ever consulted in the Daily.
/// </summary>
public static class DailyPowerGrant
{
    private static readonly PowerId[] GiftPool =
    {
        PowerId.JaguarSlam, PowerId.QuetzalFeather, PowerId.TzolkinRewind, PowerId.KukulkansCall
    };

    private static int seed;

    /// <summary>True when today's ritual runs the power meter.</summary>
    public static bool Active { get; private set; }

    /// <summary>True for Gift of the Gods: each full meter rolls a new power.</summary>
    public static bool RandomOnFill { get; private set; }

    /// <summary>The ritual's power when it isn't random.</summary>
    public static PowerId Fixed { get; private set; }

    public static bool AppliesTo(GameMode mode) => Active && mode == GameMode.DailyChallenge;

    public static void Set(bool active, PowerId fixedPower, bool randomOnFill, int dayNumberUtc)
    {
        Active = active;
        Fixed = fixedPower;
        RandomOnFill = active && randomOnFill;
        seed = dayNumberUtc;
    }

    public static void Clear()
    {
        Active = false;
        RandomOnFill = false;
        Fixed = PowerId.JaguarSlam;
        seed = 0;
    }

    /// <summary>
    /// The power granted by the <paramref name="fillIndex"/>-th full meter (0 = first).
    /// Never the same power twice in a row, so every gift feels like a new one.
    /// </summary>
    public static PowerId Gift(int fillIndex)
    {
        if (fillIndex < 0) fillIndex = 0;

        int previous = -1;
        int pick = 0;
        for (int i = 0; i <= fillIndex; i++)
        {
            pick = (int)(Hash(seed, i) % (uint)GiftPool.Length);
            if (pick == previous) pick = (pick + 1 + (int)(Hash(seed, i + 7919) % (uint)(GiftPool.Length - 1))) % GiftPool.Length;
            previous = pick;
        }
        return GiftPool[pick];
    }

    private static uint Hash(int a, int b)
    {
        unchecked
        {
            uint h = (uint)(a * 0x27D4EB2D) ^ (uint)(b * 0x165667B1) ^ 0x9E3779B9u;
            h ^= h >> 16;
            h *= 0x85EBCA6B;
            h ^= h >> 13;
            h *= 0xC2B2AE35;
            h ^= h >> 16;
            return h;
        }
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Clear();
}
