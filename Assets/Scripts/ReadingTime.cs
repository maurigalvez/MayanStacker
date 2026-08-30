using UnityEngine;

/// <summary>
/// How long a line of UI copy has to stay on screen to actually be read.
///
/// Instruction and tutorial lines used to hold for a flat 3 seconds regardless of how much
/// they said, which is fine for "Skip" and much too quick for two lines a first-time player
/// is reading while a block swings overhead. This scales the hold with the length of the
/// copy instead, so longer lines — and the longer translations of them — get the time they
/// need without anyone re-tuning a constant per string.
///
/// CJK says the same thing in far fewer characters, so the floor, not the per-character
/// rate, is what carries those locales.
/// </summary>
public static class ReadingTime
{
    /// <summary>No line is ever on screen for less than this, however short it is.</summary>
    public const float DefaultMinimum = 3.5f;

    /// <summary>Nothing overstays this, even a long line in a wordy locale.</summary>
    public const float Maximum = 9f;

    // Roughly 200 words per minute, which is a slow, distracted read — the right target for
    // someone learning a game rather than reading prose.
    private const float SecondsPerCharacter = 0.06f;

    // The beat before reading starts: noticing the line appeared and looking at it.
    private const float NoticeSeconds = 1.1f;

    public static float For(string text) => For(text, DefaultMinimum);

    public static float For(string text, float minimum)
    {
        int length = string.IsNullOrEmpty(text) ? 0 : text.Length;
        return Mathf.Clamp(NoticeSeconds + length * SecondsPerCharacter, minimum, Maximum);
    }
}
