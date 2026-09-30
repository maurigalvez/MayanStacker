/// <summary>
/// Which <see cref="LevelData"/> the temple rules read this run.
///
/// In a temple it's the temple's own asset. In the Daily it's the ritual's rule level
/// (<see cref="DailyRitual.BuildRuleLevel"/>), so the rules run in the Ritual of The Sun
/// without knowing anything about it. Infinite has none.
/// </summary>
public static class TempleRuleContext
{
    private static LevelData dailyLevel;

    /// <summary>Set by <see cref="DailyChallengeManager"/> when a ritual starts; null clears it.</summary>
    public static void SetDaily(LevelData level)
    {
        if (dailyLevel != null && dailyLevel != level && dailyLevel.hideFlags == UnityEngine.HideFlags.DontSave)
        {
            UnityEngine.Object.Destroy(dailyLevel);
        }
        dailyLevel = level;
    }

    /// <summary>The Daily's rule level, or null.</summary>
    public static LevelData DailyLevel => dailyLevel;

    /// <summary>The rule level for <paramref name="mode"/>, or null when the mode runs no rules.</summary>
    public static LevelData For(GameMode mode, LevelManager levelManager)
    {
        switch (mode)
        {
            case GameMode.StackerLevels: return levelManager != null ? levelManager.CurrentLevel : null;
            case GameMode.DailyChallenge: return dailyLevel;
            default: return null;
        }
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => dailyLevel = null;
}
