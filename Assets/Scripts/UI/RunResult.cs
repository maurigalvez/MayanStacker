using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the game-over card says about a run, decided from the run's numbers alone.
///
/// The old panel showed a bare score, so a run that fell early and a run that nearly beat the
/// best looked identical. Here every run gets a mood - and the mood picks the headline, the
/// flavour line and the colour - so the card always answers "how did that go, and what now?".
///
/// Pure: <see cref="Build"/> reads only its input, which keeps the rules easy to reason about
/// and leaves gathering the numbers to UIManager.
/// </summary>
public static class RunResult
{
    public enum Mood
    {
        /// <summary>Beat a real previous best.</summary>
        NewBest,
        /// <summary>First scoring run ever - there was no best to beat.</summary>
        First,
        /// <summary>Within reach of the best, or of the level/daily target.</summary>
        Near,
        /// <summary>A respectable run that fell short.</summary>
        Solid,
        /// <summary>Fell within the first few stones.</summary>
        Early,
        /// <summary>Daily ritual finished.</summary>
        Complete,
        /// <summary>Daily ritual broken.</summary>
        Broken
    }

    public struct Input
    {
        public GameMode mode;
        public int score;
        public int blocks;
        public int perfectLandings;
        public int maxCombo;
        public int perfectHitsRequired;

        /// <summary>Infinite: the high score when the run began.</summary>
        public int bestBefore;

        /// <summary>Levels: blocks needed to claim the site.</summary>
        public int levelRequired;

        /// <summary>Daily: whether the block cap was reached.</summary>
        public bool dailyCompleted;
        public int dailyTarget;
        /// <summary>Daily: today's best before this run was recorded.</summary>
        public int dailyBestBefore;

        /// <summary>A concrete goal from achievements/map progress, or null.</summary>
        public string progressGoal;

        /// <summary>Whether the Serpent's Edge has been introduced, so its tip means something.</summary>
        public bool edgeUnlocked;

        /// <summary>Serpent's Edge stones that landed this run, and what the edge added and cost.</summary>
        public int edgeLanded;
        public int edgeBonusPoints;
        public int edgeCombosBroken;

        /// <summary>Seed for picking among flavour lines and tips.</summary>
        public int seed;
    }

    public struct Card
    {
        public Mood mood;
        public string headline;
        public string flavour;

        /// <summary>The number the score counts up to.</summary>
        public int scoreValue;
        /// <summary>When above 0 the score reads "value / scoreOutOf" (Levels).</summary>
        public int scoreOutOf;

        /// <summary>Best/target context under the score. Null hides the line.</summary>
        public string bestLine;
        /// <summary>True when the best line reports a record, so it gets the celebration colour.</summary>
        public bool bestLineCelebrates;

        public string height;
        public string perfects;
        public string combo;

        /// <summary>Always set - falls back to a skill tip.</summary>
        public string goal;

        /// <summary>What the Serpent's Edge earned (and broke) this run. Null when it wasn't taken.</summary>
        public string edgeLine;
    }

    /// <summary>A fall at or below this many stones reads as an early one.</summary>
    public const int EarlyFallBlocks = 5;

    public static Card Build(Input input)
    {
        var random = new System.Random(input.seed);
        var card = new Card
        {
            scoreValue = Mathf.Max(0, input.score),
            height = input.blocks.ToString(),
            perfects = input.perfectLandings.ToString(),
            combo = LocalizationManager.Get("result_combo_value", input.maxCombo)
        };

        switch (input.mode)
        {
            case GameMode.StackerLevels:
                BuildLevel(ref card, input, random);
                break;
            case GameMode.DailyChallenge:
                BuildDaily(ref card, input, random);
                break;
            default:
                BuildInfinite(ref card, input, random);
                break;
        }

        card.goal = !string.IsNullOrEmpty(input.progressGoal) ? input.progressGoal : PickTip(input, random);

        // Only when the edge was taken: the run's own receipt is what teaches whether it paid.
        if (input.edgeLanded > 0)
        {
            card.edgeLine = input.edgeCombosBroken > 0
                ? LocalizationManager.Get("result_edge_line_broke", input.edgeLanded, input.edgeBonusPoints, input.edgeCombosBroken)
                : LocalizationManager.Get("result_edge_line", input.edgeLanded, input.edgeBonusPoints);
        }
        return card;
    }

    private static void BuildInfinite(ref Card card, Input input, System.Random random)
    {
        int score = input.score;
        int best = input.bestBefore;

        if (best > 0 && score > best)
        {
            SetMood(ref card, Mood.NewBest, "result_head_new_best", Flavour("new_best", random));
            card.bestLine = LocalizationManager.Get("result_best_beaten", best, score - best);
            card.bestLineCelebrates = true;
            return;
        }

        if (best <= 0 && score > 0)
        {
            // Nothing stored yet, so this run *is* the best - but calling a first attempt a
            // "new high score" is what made the old panel feel random.
            SetMood(ref card, Mood.First, "result_head_first", "result_flavor_first");
            return;
        }

        int gap = best - score;
        if (best > 0 && gap <= Mathf.Max(50, best / 5))
        {
            SetMood(ref card, Mood.Near, "result_head_near", Flavour("near", random));
            card.bestLine = gap > 0
                ? LocalizationManager.Get("result_best_gap", best, gap)
                : LocalizationManager.Get("result_best", best);
            return;
        }

        SetFallMood(ref card, input.blocks, random);
        if (best > 0) card.bestLine = LocalizationManager.Get("result_best", best);
    }

    private static void BuildLevel(ref Card card, Input input, System.Random random)
    {
        int required = Mathf.Max(1, input.levelRequired);
        int remaining = Mathf.Max(1, required - input.blocks);

        card.scoreValue = Mathf.Max(0, input.blocks);
        card.scoreOutOf = required;
        card.bestLine = LocalizationManager.Get("result_level_to_go", remaining);

        if (remaining <= Mathf.Max(2, required / 4))
        {
            SetMood(ref card, Mood.Near, "result_head_level_near", Flavour("near", random));
        }
        else
        {
            SetFallMood(ref card, input.blocks, random);
        }
    }

    private static void BuildDaily(ref Card card, Input input, System.Random random)
    {
        if (input.dailyCompleted)
        {
            SetMood(ref card, Mood.Complete, "daily_result_complete", "daily_result_complete_flavor");
        }
        else
        {
            string flavour;
            if (input.dailyTarget > 0 && input.blocks * 4 >= input.dailyTarget * 3) flavour = Flavour("near", random);
            else if (input.blocks <= EarlyFallBlocks) flavour = Flavour("early", random);
            else flavour = Flavour("solid", random);

            SetMood(ref card, Mood.Broken, "daily_result_broken", flavour);
        }

        int before = input.dailyBestBefore;
        if (before > 0 && input.score > before)
        {
            card.bestLine = LocalizationManager.Get("result_daily_best_beaten", input.score - before);
            card.bestLineCelebrates = true;
        }
        else if (before > 0)
        {
            card.bestLine = LocalizationManager.Get("result_daily_best", before);
        }
    }

    private static void SetFallMood(ref Card card, int blocks, System.Random random)
    {
        if (blocks <= EarlyFallBlocks)
        {
            SetMood(ref card, Mood.Early, "result_head_early", Flavour("early", random));
        }
        else
        {
            SetMood(ref card, Mood.Solid, "result_head_solid", Flavour("solid", random));
        }
    }

    private static void SetMood(ref Card card, Mood mood, string headlineKey, string flavourKey)
    {
        card.mood = mood;
        card.headline = LocalizationManager.Get(headlineKey);
        card.flavour = LocalizationManager.Get(flavourKey);
    }

    private const int FlavourVariants = 3;

    private static string Flavour(string pool, System.Random random)
    {
        return "result_flavor_" + pool + "_" + (random.Next(FlavourVariants) + 1);
    }

    private static string PickTip(Input input, System.Random random)
    {
        var tips = new List<string>(4)
        {
            LocalizationManager.Get("result_tip_straighten", Mathf.Max(1, input.perfectHitsRequired)),
            LocalizationManager.Get("result_tip_combo"),
            LocalizationManager.Get("result_tip_align")
        };

        // Only once the player has met the Edge - before that it names something they can't see.
        if (input.edgeUnlocked) tips.Add(LocalizationManager.Get("result_tip_edge"));

        return tips[random.Next(tips.Count)];
    }
}
