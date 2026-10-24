using System.Collections;
using UnityEngine;

/// <summary>
/// Decides which main-menu rail features the player can see yet. A returning player must not land on
/// five glowing badges and a jade bar at once: the rail opens one feature at a time, in an order that
/// teaches the economy (the market and its balance first, then the daily habits, then the long
/// tracks), and each new one arrives with a cue until it has been opened.
///
/// A feature is revealed when the player has been through the first temple and either
///   • it is the first, or
///   • a new session has started and they have finished a run since the last reveal, or
///   • they have finished three runs since the last reveal.
/// So a daily player meets one new thing per visit and a binge player at most one per few runs, and
/// nobody meets all five on the day the update lands. Chips ask <see cref="IsRevealed"/> from their
/// Refresh and light their cue with <see cref="IsFresh"/> until <see cref="Introduced"/>.
///
/// State is PlayerPrefs, local; nothing here touches a score.
/// </summary>
public static class MenuRailPacing
{
    public enum Feature
    {
        Market = 0,
        Chest = 1,
        Missions = 2,
        Path = 3,
        Expedition = 4,
    }

    private const int FeatureCount = 5;
    private const int RunsToForceReveal = 3;
    private const int RunsThatMakeAnEstablishedPlayer = 8;

    private const string RevealedKey = "rail_revealed";
    private const string RevealRunsKey = "rail_reveal_runs";
    private const string RevealSessionKey = "rail_reveal_session";
    private const string FreshKey = "rail_fresh";
    private const string AnnouncedKey = "rail_announced";
    private const float AnnounceDelay = 1.4f;

    private static int evaluatedStamp = -1;

    /// <summary>How many features have been revealed so far (in <see cref="Feature"/> order).</summary>
    public static int RevealedCount
    {
        get
        {
            Evaluate();
            return PlayerPrefs.GetInt(RevealedKey, 0);
        }
    }

    public static bool IsRevealed(Feature feature)
    {
        return (int)feature < RevealedCount;
    }

    /// <summary>True from the moment a feature is revealed until the player opens it.</summary>
    public static bool IsFresh(Feature feature)
    {
        Evaluate();
        return (PlayerPrefs.GetInt(FreshKey, 0) & (1 << (int)feature)) != 0;
    }

    /// <summary>The player opened the feature: its "new" cue stops.</summary>
    public static void Introduced(Feature feature)
    {
        int fresh = PlayerPrefs.GetInt(FreshKey, 0);
        int cleared = fresh & ~(1 << (int)feature);
        if (cleared == fresh) return;
        PlayerPrefs.SetInt(FreshKey, cleared);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Tells the player, once, what the newly revealed feature is and how to take part: its name and one
    /// plain line, as a banner after the menu has settled. Chips start this when they appear.
    /// </summary>
    public static IEnumerator AnnounceLater(Feature feature)
    {
        if (!IsFresh(feature)) yield break;
        int bit = 1 << (int)feature;
        if ((PlayerPrefs.GetInt(AnnouncedKey, 0) & bit) != 0) yield break;

        float waited = 0f;
        while (waited < AnnounceDelay) { waited += Time.unscaledDeltaTime; yield return null; }

        // Another chip may have announced it meanwhile, or the player may have opened it already.
        if (!IsFresh(feature) || (PlayerPrefs.GetInt(AnnouncedKey, 0) & bit) != 0) yield break;

        // One announcement per visit: the release card owns this one, and the banner returns next visit.
        if (ReleaseAnnouncement.ShownThisSession) yield break;
        PlayerPrefs.SetInt(AnnouncedKey, PlayerPrefs.GetInt(AnnouncedKey, 0) | bit);
        PlayerPrefs.Save();
        RunBanner.Show(LocalizationManager.Get("rail_new_" + feature + "_title"),
            LocalizationManager.Get("rail_new_" + feature + "_sub"), RunOverlayUI.Gold, 3.4f, -100f);
    }

    /// <summary>Raised when features are revealed outside the normal pacing, so visible chips can refresh.</summary>
    public static event System.Action RevealChanged;

    /// <summary>
    /// Reveals every feature up to and including <paramref name="feature"/> right now, each with its
    /// cue, and without their own banners (the caller is already telling the player about them). Used
    /// when something else sends the player to a feature, like the release card's chest bonus.
    /// </summary>
    public static void RevealThrough(Feature feature)
    {
        int target = (int)feature + 1;
        int revealed = PlayerPrefs.GetInt(RevealedKey, 0);
        if (revealed >= target) return;

        int bits = 0;
        for (int i = revealed; i < target; i++) bits |= 1 << i;
        PlayerPrefs.SetInt(FreshKey, PlayerPrefs.GetInt(FreshKey, 0) | bits);
        PlayerPrefs.SetInt(AnnouncedKey, PlayerPrefs.GetInt(AnnouncedKey, 0) | bits);
        PlayerPrefs.SetInt(RevealedKey, target);
        PlayerPrefs.SetInt(RevealRunsKey, FtueState.LifetimeRuns);
        PlayerPrefs.SetInt(RevealSessionKey, FtueState.SessionNumber);
        PlayerPrefs.Save();
        evaluatedStamp = -1;
        RevealChanged?.Invoke();
    }

    private static void Evaluate()
    {
        int stamp = FtueState.SessionNumber * 100000 + FtueState.LifetimeRuns;
        if (stamp == evaluatedStamp) return;
        evaluatedStamp = stamp;

        int revealed = PlayerPrefs.GetInt(RevealedKey, 0);
        if (revealed >= FeatureCount) return;

        // Nothing competes with the first temple: the rail stays empty until it is cleared. A player
        // who has plainly been around a while (runs on record) is never held back by a missing flag.
        bool established = FtueState.ShouldPromoteReturningPlayerFeatures ||
                           FtueState.LifetimeRuns >= RunsThatMakeAnEstablishedPlayer;
        if (!established) return;

        int runsSince = FtueState.LifetimeRuns - PlayerPrefs.GetInt(RevealRunsKey, 0);
        bool newSession = PlayerPrefs.GetInt(RevealSessionKey, -1) != FtueState.SessionNumber;
        bool due = revealed == 0 ||
                   (newSession && runsSince >= 1) ||
                   runsSince >= RunsToForceReveal;
        if (!due) return;

        PlayerPrefs.SetInt(FreshKey, PlayerPrefs.GetInt(FreshKey, 0) | (1 << revealed));
        PlayerPrefs.SetInt(RevealedKey, revealed + 1);
        PlayerPrefs.SetInt(RevealRunsKey, FtueState.LifetimeRuns);
        PlayerPrefs.SetInt(RevealSessionKey, FtueState.SessionNumber);
        PlayerPrefs.Save();
    }

    /// <summary>Forgets the pacing. Full data resets only.</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(RevealedKey);
        PlayerPrefs.DeleteKey(RevealRunsKey);
        PlayerPrefs.DeleteKey(RevealSessionKey);
        PlayerPrefs.DeleteKey(FreshKey);
        PlayerPrefs.DeleteKey(AnnouncedKey);
        PlayerPrefs.Save();
        evaluatedStamp = -1;
    }

    /// <summary>Shows every feature at once. For debug windows and tests.</summary>
    public static void RevealAll()
    {
        PlayerPrefs.SetInt(RevealedKey, FeatureCount);
        PlayerPrefs.SetInt(FreshKey, 0);
        PlayerPrefs.Save();
        evaluatedStamp = -1;
    }
}
