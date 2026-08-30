using UnityEngine;

/// <summary>
/// The boons in effect right now, and the counters that expire them.
///
/// Static for the same reason <see cref="RunModifierService"/> is: a boon applies to the
/// whole run, is read from several unrelated systems (scoring, spawning, the combo rules)
/// and dies with the run. Keeping it here means none of those systems needs a reference to
/// the boon UI, and none of them changes behaviour when boons are switched off — every
/// getter returns a neutral value with nothing granted.
///
/// Effects tick on gameplay events rather than on time, so a player who puts the phone
/// down mid-run doesn't lose a boon to the clock.
/// </summary>
public static class ActiveBoons
{
    private static float scoreMultiplier = 1f;
    private static int scoreBlocksRemaining;

    private static float widthMultiplier = 1f;
    private static int widthBlocksRemaining;

    private static int comboShields;

    /// <summary>Score multiplier from boons. 1 when nothing is active.</summary>
    public static float ScoreMultiplier => scoreBlocksRemaining > 0 ? scoreMultiplier : 1f;

    /// <summary>Block width multiplier from boons. 1 when nothing is active.</summary>
    public static float WidthMultiplier => widthBlocksRemaining > 0 ? widthMultiplier : 1f;

    /// <summary>How many combo breaks are still absorbed.</summary>
    public static int ComboShields => comboShields;

    /// <summary>True when any boon is currently doing something.</summary>
    public static bool AnyActive =>
        scoreBlocksRemaining > 0 || widthBlocksRemaining > 0 || comboShields > 0;

    /// <summary>Blocks left on the score boon, for HUD use.</summary>
    public static int ScoreBlocksRemaining => scoreBlocksRemaining;

    /// <summary>Blocks left on the width boon, for HUD use.</summary>
    public static int WidthBlocksRemaining => widthBlocksRemaining;

    /// <summary>
    /// Applies a chosen boon.
    ///
    /// Re-granting a boon that's already running refreshes its duration rather than
    /// stacking the multiplier — stacking would let a lucky offer sequence run away with
    /// the score, and refreshing is what players expect anyway.
    /// </summary>
    public static void Grant(BoonId id)
    {
        BoonDefinition def = BoonDefinition.For(id);

        if (def.scoreMultiplier > 1f && def.durationInBlocks > 0)
        {
            scoreMultiplier = def.scoreMultiplier;
            scoreBlocksRemaining = def.durationInBlocks;
        }

        if (def.widthMultiplier > 1f && def.durationInBlocks > 0)
        {
            widthMultiplier = def.widthMultiplier;
            widthBlocksRemaining = def.durationInBlocks;
        }

        if (def.comboShields > 0)
        {
            comboShields += def.comboShields;
        }

        if (def.straightensStack)
        {
            var gameManager = DependencyRegistry.Find<GameManager>();
            if (gameManager != null)
            {
                gameManager.TriggerKukulkanShift();
            }
        }

        Debug.Log($"[Boon] Granted '{id}'.");
    }

    /// <summary>
    /// Consumes one combo shield. Returns true when a break was absorbed.
    /// Called by GameManager instead of breaking the combo on a poor landing.
    /// </summary>
    public static bool TryConsumeComboShield()
    {
        if (comboShields <= 0) return false;

        comboShields--;
        Debug.Log($"[Boon] Jade Eye absorbed a combo break ({comboShields} left).");
        return true;
    }

    /// <summary>Ticks the score boon. Called once per scored landing.</summary>
    public static void RegisterBlockScored()
    {
        if (scoreBlocksRemaining > 0)
        {
            scoreBlocksRemaining--;
            if (scoreBlocksRemaining == 0) scoreMultiplier = 1f;
        }
    }

    /// <summary>Ticks the width boon. Called once per block the spawner creates.</summary>
    public static void RegisterBlockSpawned()
    {
        if (widthBlocksRemaining > 0)
        {
            widthBlocksRemaining--;
            if (widthBlocksRemaining == 0) widthMultiplier = 1f;
        }
    }

    /// <summary>Clears everything. Called at the start of every run.</summary>
    public static void ResetRun()
    {
        scoreMultiplier = 1f;
        scoreBlocksRemaining = 0;
        widthMultiplier = 1f;
        widthBlocksRemaining = 0;
        comboShields = 0;
    }

    /// <summary>
    /// Wipes state before the first scene loads, so "Enter Play Mode without domain
    /// reload" can't leak a boon from a previous editor session into a new run.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ResetRun();
    }
}
