using UnityEngine;

/// <summary>
/// Picks the one moment of a run worth bragging about and words it for the share message.
/// Pure: reads the finished card and the run's counters, nothing is stored.
/// Priority: a new best, a Clutch Save, a Razor Edge, a long Perfect chain, then plain height.
/// </summary>
public static class RunHighlight
{
    public enum Kind { Tower, Chain, RazorEdge, ClutchSave, NewBest }

    public static Kind Pick(RunResult.Mood mood, int clutchSaves, int razorEdges, int maxCombo, int chainMoment)
    {
        if (mood == RunResult.Mood.NewBest) return Kind.NewBest;
        if (clutchSaves > 0) return Kind.ClutchSave;
        if (razorEdges > 0) return Kind.RazorEdge;
        if (maxCombo >= chainMoment) return Kind.Chain;
        return Kind.Tower;
    }

    /// <summary>The localized moment phrase, e.g. "a Clutch Save".</summary>
    public static string Describe(Kind kind, int maxCombo)
    {
        switch (kind)
        {
            case Kind.NewBest: return LocalizationManager.Get("share_moment_best");
            case Kind.ClutchSave: return LocalizationManager.Get("share_moment_clutch");
            case Kind.RazorEdge: return LocalizationManager.Get("share_moment_razor");
            case Kind.Chain: return LocalizationManager.Get("share_moment_chain", maxCombo);
            default: return LocalizationManager.Get("share_moment_tower");
        }
    }

    /// <summary>The full message: stones, the moment, and the store link when one is set.</summary>
    public static string BuildMessage(RunResult.Card card, int maxCombo, ShareSettings settings)
    {
        Kind kind = Pick(card.mood, GameFeelManager.ClutchSavesThisRun, GameFeelManager.RazorEdgesThisRun,
            maxCombo, settings.chainMoment);

        string text = LocalizationManager.Get("share_text", card.height, Describe(kind, maxCombo));
        if (!string.IsNullOrEmpty(settings.storeUrlFormat))
        {
            text += "\n" + string.Format(settings.storeUrlFormat, Application.identifier);
        }
        return text;
    }
}
