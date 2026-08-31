using UnityEngine;

/// <summary>
/// The random stream every run-shaping decision draws from.
///
/// Block variety and boon offers used to roll off <see cref="UnityEngine.Random"/>, which
/// meant replaying the same level — or two players opening the same Daily — got different
/// runs. That reads as luck rather than design: a failed level retry was partly a re-roll,
/// and a shared Daily wasn't actually the same challenge for everyone.
///
/// So the stream is seeded per mode:
///   - <see cref="GameMode.StackerLevels"/> seeds from the level number, so a level plays
///     the same way every attempt and can be learned.
///   - <see cref="GameMode.DailyChallenge"/> seeds from the UTC day number, so every player
///     on a given day faces an identical run.
///   - <see cref="GameMode.InfiniteStacker"/> is deliberately left unseeded — variance is
///     the whole appeal of endless, and a fixed sequence would make run 20 identical to
///     run 1.
///
/// When no seed is in play every call falls straight through to <see cref="UnityEngine.Random"/>,
/// so Infinite behaves exactly as it did before this existed.
///
/// Only decisions the player can feel should draw from here. Cosmetics — audio pitch,
/// particle colours, environment spawns — must stay on Unity's stream: routing them
/// through the seeded one adds draws whose count can change with an unrelated tweak,
/// which would silently shift every level's block sequence.
///
/// State is static because a run's randomness is global and short-lived, matching
/// <see cref="RunModifierService"/> and <see cref="ActiveBoons"/>.
/// </summary>
public static class RunRandom
{
    // Mixed into every seed so a future change to the sequence can be rolled out
    // deliberately: bump this and every level draws a fresh (but still fixed) run.
    private const int SeedVersion = 1;

    // Distinct per mode so level 12 and day 12 don't share a sequence.
    private const int LevelSalt = unchecked((int)0x9E3779B9);
    private const int DailySalt = unchecked((int)0x85EBCA6B);

    private static System.Random stream;
    private static int currentSeed;

    /// <summary>True while a seeded run is in progress. False means Unity's global stream.</summary>
    public static bool IsDeterministic => stream != null;

    /// <summary>The seed of the current run, or 0 when unseeded. Diagnostics only.</summary>
    public static int CurrentSeed => currentSeed;

    /// <summary>
    /// Opens the stream for a run. Called once from <see cref="GameManager.StartGame"/>,
    /// before anything can roll, so a restart replays the identical sequence.
    /// </summary>
    /// <param name="mode">The mode being started; decides whether a seed applies at all.</param>
    /// <param name="levelNumber">Level number for <see cref="GameMode.StackerLevels"/>, or -1.</param>
    /// <param name="dayNumberUtc">
    /// Days since 1970-01-01 UTC for <see cref="GameMode.DailyChallenge"/>. Pass the value
    /// from the fetched config when there is one, so the seed follows the server's day
    /// rather than a device clock.
    /// </param>
    public static void BeginRun(GameMode mode, int levelNumber = -1, int dayNumberUtc = -1)
    {
        switch (mode)
        {
            case GameMode.StackerLevels when levelNumber > 0:
                Seed(Mix(LevelSalt, levelNumber));
                break;

            case GameMode.DailyChallenge when dayNumberUtc >= 0:
                Seed(Mix(DailySalt, dayNumberUtc));
                break;

            default:
                // Infinite, or a mode whose identity we couldn't resolve. An unknown level
                // number is better served by honest randomness than by a shared seed that
                // would make every such run identical to every other.
                Clear();
                break;
        }
    }

    /// <summary>Closes the stream, returning every caller to Unity's global randomness.</summary>
    public static void EndRun()
    {
        Clear();
    }

    /// <summary>A float in [0, 1), matching <see cref="UnityEngine.Random.value"/> closely enough for weighted picks.</summary>
    public static float Value =>
        stream != null ? (float)stream.NextDouble() : Random.value;

    /// <summary>An int in [minInclusive, maxExclusive), matching <see cref="UnityEngine.Random.Range(int,int)"/>.</summary>
    public static int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;

        return stream != null
            ? stream.Next(minInclusive, maxExclusive)
            : Random.Range(minInclusive, maxExclusive);
    }

    /// <summary>A float in [min, max), matching <see cref="UnityEngine.Random.Range(float,float)"/>.</summary>
    public static float Range(float min, float max)
    {
        if (max <= min) return min;

        return stream != null
            ? min + (float)stream.NextDouble() * (max - min)
            : Random.Range(min, max);
    }

    private static void Seed(int seed)
    {
        currentSeed = seed;
        stream = new System.Random(seed);
    }

    private static void Clear()
    {
        currentSeed = 0;
        stream = null;
    }

    // Cheap avalanche (the finalizer from MurmurHash3) so adjacent level numbers don't
    // produce adjacent — and therefore similar-feeling — sequences.
    private static int Mix(int salt, int value)
    {
        unchecked
        {
            uint h = (uint)(salt ^ (value * 0x27D4EB2D) ^ (SeedVersion * 0x165667B1));
            h ^= h >> 16;
            h *= 0x85EBCA6B;
            h ^= h >> 13;
            h *= 0xC2B2AE35;
            h ^= h >> 16;

            // System.Random rejects int.MinValue's magnitude; keep the seed non-negative.
            return (int)(h & 0x7FFFFFFF);
        }
    }
}
