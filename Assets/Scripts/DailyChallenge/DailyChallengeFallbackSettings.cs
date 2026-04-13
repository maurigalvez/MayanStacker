using UnityEngine;

/// <summary>
/// Default Daily Challenge config used when PlayFab Title Data is unavailable
/// (first run offline, network failure, etc.). Loaded from
/// Resources/DailyChallenge/DailyChallengeFallbackSettings.
/// </summary>
[CreateAssetMenu(
    fileName = "DailyChallengeFallbackSettings",
    menuName = "TamalStacker/Daily Challenge Fallback Settings",
    order = 1)]
public class DailyChallengeFallbackSettings : ScriptableObject
{
    [Tooltip("Block count target when Title Data is unavailable.")]
    public int defaultBlockCount = 30;

    [Tooltip("Modifier rotation used when Title Data is unavailable. The same dayNumber % length rotation applies.")]
    public DailyChallengeModifier[] defaultModifiers = new[]
    {
        DailyChallengeModifier.SpeedRun,
        DailyChallengeModifier.FragileStack,
        DailyChallengeModifier.ComboChain
    };
}
