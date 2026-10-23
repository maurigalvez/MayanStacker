using UnityEngine;

/// <summary>
/// First-time lessons for the Spins systems (Swing Moods, Sun Surge), taught through the
/// <see cref="GuideLane"/> like every other lesson: one per run, on a calm beat, and only
/// marked seen once the lane actually accepted it. A refused lesson asks again next run.
/// </summary>
public static class SpinLessons
{
    public const string MoodLessonId = "swing_mood";
    public const string SurgeLessonId = "sun_surge";

    private const string PP_MOOD_SEEN = "Ftue_SpinMoodSeen";
    private const string PP_SURGE_SEEN = "Ftue_SpinSurgeSeen";

    private const float HoldSeconds = 2.6f;

    public static bool HasSeenMood => PlayerPrefs.GetInt(PP_MOOD_SEEN, 0) == 1;
    public static bool HasSeenSurge => PlayerPrefs.GetInt(PP_SURGE_SEEN, 0) == 1;

    /// <summary>A stone just rolled a mood and its cue is glowing: say what the glow means.</summary>
    public static void OnMoodRolled()
    {
        if (HasSeenMood || !GuideLane.CanTeach(MoodLessonId)) return;

        if (!GuideLane.TryTeach(MoodLessonId,
                LocalizationManager.Get("lesson_mood_title"),
                LocalizationManager.Get("lesson_mood_body"),
                RunOverlayUI.Gold,
                HoldSeconds)) return;

        Mark(PP_MOOD_SEEN, "spin_mood_intro_shown");
    }

    /// <summary>The Sun Surge just ignited: say what is happening and that it is temporary.</summary>
    public static void OnSurgeIgnited()
    {
        if (HasSeenSurge || !GuideLane.CanTeach(SurgeLessonId)) return;

        if (!GuideLane.TryTeach(SurgeLessonId,
                LocalizationManager.Get("lesson_surge_title"),
                LocalizationManager.Get("lesson_surge_body"),
                RunOverlayUI.Gold,
                HoldSeconds)) return;

        Mark(PP_SURGE_SEEN, "spin_surge_intro_shown");
    }

    /// <summary>Forgets both lessons (new-game reset).</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(PP_MOOD_SEEN);
        PlayerPrefs.DeleteKey(PP_SURGE_SEEN);
    }

    private static void Mark(string key, string analyticsEvent)
    {
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        GameAnalytics.Track(analyticsEvent);
    }
}
