using UnityEngine;

/// <summary>
/// The "what's new" card shown once per release to players who were already stacking before it, and
/// the thank-you that comes with it: for <see cref="LoginChestSettings.returningBonusDays"/> days
/// from the moment the card appears, every login chest pays <see cref="LoginChestSettings.returningBonusMultiplier"/>
/// times its jade.
///
/// Who sees it: anyone who has run a temple before the update. A new install is marked as having seen
/// it on its first menu visit (<see cref="Evaluate"/>), so a newcomer meets the features one by one
/// through <see cref="MenuRailPacing"/> instead of reading a list of things they have never had.
///
/// The card is a single prompt: it never shows in the first session, never during the tutorial, and
/// the rail's own "new" banners stand down for the visit it appears on. State is PlayerPrefs, local.
/// </summary>
public static class ReleaseAnnouncement
{
    /// <summary>The release this card announces. Bump it (and the loc copy) for the next one.</summary>
    public const string Version = "1.6.0";

    private const string SeenKey = "ReleaseAnnounce_Seen";
    private const string BonusStartKey = "ReleaseAnnounce_BonusStart";

    /// <summary>True once the card has been put on screen in this app session.</summary>
    public static bool ShownThisSession { get; private set; }

    private static bool SeenThisRelease => PlayerPrefs.GetString(SeenKey, string.Empty) == Version;

    private static int Today => DailyChallengeManager.CurrentDayNumberUtc();

    /// <summary>Marks a brand-new install as having seen the card. Call on every menu entry.</summary>
    public static void Evaluate()
    {
        if (SeenThisRelease) return;
        if (FtueState.LifetimeRuns > 0 || FtueState.HasCompletedFirstTemple) return;
        PlayerPrefs.SetString(SeenKey, Version);
        PlayerPrefs.Save();
    }

    /// <summary>The card is due: a returning player past the tutorial who has not seen this release's card.</summary>
    public static bool ShouldShow =>
        !ShownThisSession && !SeenThisRelease && FtueState.ShouldPromoteReturningPlayerFeatures;

    /// <summary>Whether this build has a chest bonus to offer (the chest and jade must both be on).</summary>
    public static bool BonusOffered
    {
        get
        {
            var s = LoginChestSettings.Active;
            return LoginChest.Available && s != null && s.returningBonusMultiplier > 1 && s.returningBonusDays > 0;
        }
    }

    public static int BonusDays
    {
        get
        {
            var s = LoginChestSettings.Active;
            return s != null ? Mathf.Max(0, s.returningBonusDays) : 0;
        }
    }

    /// <summary>
    /// The card went on screen: remember it, start the bonus window, and make sure the chest is on the
    /// menu rail so the player can actually go and claim it (the rail would otherwise pace it in later).
    /// </summary>
    public static void MarkShown()
    {
        ShownThisSession = true;
        PlayerPrefs.SetString(SeenKey, Version);
        if (BonusOffered && PlayerPrefs.GetInt(BonusStartKey, int.MinValue) == int.MinValue)
        {
            PlayerPrefs.SetInt(BonusStartKey, Today);
            MenuRailPacing.RevealThrough(MenuRailPacing.Feature.Chest);
        }
        PlayerPrefs.Save();
    }

    /// <summary>Multiplier on a chest opened <paramref name="dayOffset"/> days from today (1 outside the window).</summary>
    public static int ChestMultiplier(int dayOffset = 0)
    {
        var s = LoginChestSettings.Active;
        int start = PlayerPrefs.GetInt(BonusStartKey, int.MinValue);
        if (s == null || start == int.MinValue) return 1;
        int day = Today + dayOffset;
        return day >= start && day < start + Mathf.Max(0, s.returningBonusDays)
            ? Mathf.Max(1, s.returningBonusMultiplier)
            : 1;
    }

    /// <summary>Days of the chest bonus left including today, 0 when it is not running.</summary>
    public static int BonusDaysLeft
    {
        get
        {
            if (ChestMultiplier() <= 1) return 0;
            return PlayerPrefs.GetInt(BonusStartKey) + BonusDays - Today;
        }
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        PlayerPrefs.DeleteKey(BonusStartKey);
        PlayerPrefs.Save();
        ShownThisSession = false;
    }
}
