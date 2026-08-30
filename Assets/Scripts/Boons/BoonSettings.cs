using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Tuning for how often Kukulkan offers a choice, and where.
///
/// An asset in Resources rather than fields on a scene component, so the whole system can
/// be tuned or switched off without opening a scene. If the asset is missing, boons never
/// trigger and the game plays exactly as it did before them.
/// </summary>
[CreateAssetMenu(fileName = "BoonSettings", menuName = "TamalStacker/Boon Settings", order = 4)]
public class BoonSettings : ScriptableObject
{
    /// <summary>The Resources path the boon system loads this from.</summary>
    public const string ResourcePath = "BoonSettings";

    [Header("Master switch")]
    public bool enableBoons = true;

    [Header("Cadence")]
    [Tooltip("Stack height at which the first choice is offered.")]
    [Min(1)]
    public int firstOfferAtBlock = 10;

    [Tooltip("Blocks between offers after the first.")]
    [Min(1)]
    public int blocksBetweenOffers = 10;

    [Tooltip("How many boons to put in front of the player. Three is enough to be a decision without being a menu.")]
    [Range(2, 4)]
    public int offerCount = 3;

    [Header("Where boons apply")]
    [Tooltip("Infinite runs are long enough for choices to compound.")]
    public bool applyToInfinite = true;

    [Tooltip("Daily runs are a fixed-length fairness contract - everyone gets the same run. Off by default.")]
    public bool applyToDaily = false;

    [Tooltip("Levels are short and objective-driven, so an offer usually interrupts rather than adds. Off by default.")]
    public bool applyToLevels = false;

    [Header("Telegraph")]
    [Tooltip("How many blocks before an offer the warning starts. Zero switches the warning off.")]
    [Range(0, 5)]
    public int telegraphLeadBlocks = 2;

    [Tooltip("Realtime seconds the picker is visible but untappable, so the tap that placed the last stone can't blind-pick a boon.")]
    [Range(0f, 1.5f)]
    public float armDelaySeconds = 0.45f;

    [Tooltip("Explain what boons are the first time a player is ever offered one.")]
    public bool showIntroOnFirstOffer = true;

    [Header("Guard rails")]
    [Tooltip("Never interrupt a player who is still in the tutorial and learning the core tap. " +
             "Deliberately NOT the whole FtueState.IsInFtue window - that stays true until a " +
             "temple is completed, which an Infinite-only player may never do, and boons would " +
             "then never appear for them at all.")]
    [FormerlySerializedAs("suppressDuringFtue")]
    public bool suppressDuringTutorial = true;

    [Tooltip("Realtime delay before the picker appears, so it never lands on top of hit-stop or the Kukulkan slow-motion.")]
    [Range(0f, 2f)]
    public float openDelaySeconds = 0.7f;

    /// <summary>True when this mode should offer boons at all.</summary>
    public bool AppliesTo(GameMode mode)
    {
        if (!enableBoons) return false;

        switch (mode)
        {
            case GameMode.InfiniteStacker: return applyToInfinite;
            case GameMode.DailyChallenge: return applyToDaily;
            case GameMode.StackerLevels: return applyToLevels;
            default: return false;
        }
    }

    /// <summary>True when the stack reaching <paramref name="height"/> should trigger an offer.</summary>
    public bool ShouldOfferAt(int height)
    {
        return BlocksUntilOffer(height) == 0;
    }

    /// <summary>
    /// Blocks still to place before the next offer, counting from a stack of
    /// <paramref name="height"/>. Zero means this height is itself an offer.
    ///
    /// The telegraph reads this so the warning is derived from the same cadence as the
    /// offer itself — there is no second copy of the schedule to drift out of sync.
    /// </summary>
    public int BlocksUntilOffer(int height)
    {
        if (height < firstOfferAtBlock) return firstOfferAtBlock - height;

        int step = Mathf.Max(1, blocksBetweenOffers);
        int into = (height - firstOfferAtBlock) % step;
        return into == 0 ? 0 : step - into;
    }
}
