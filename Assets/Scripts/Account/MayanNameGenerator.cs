/// <summary>
/// Builds a temple-themed display name ("Jade Itzel", "Serpiente Balam", "翡翠 Pakal") for
/// players who have no Google Play name, replacing the old "Player_XXXXXX" placeholder.
///
/// The title comes from the player's language (keys mayan_title_0..N in the localization
/// files); the Mayan name is a proper noun and stays the same in every language. Both are
/// picked from the PlayFab ID, so a player always gets the same name, and retries after a
/// failed request don't hand out a different one.
/// </summary>
public static class MayanNameGenerator
{
    public const int TitleCount = 12;

    // Latin only and no apostrophes (K'inich -> Kinich): these names show on every player's
    // leaderboard, whatever their font. Longest title + name must stay within 20 characters,
    // leaving room for the "#XXXX" tag PlayFab collisions need (display names max out at 25).
    private static readonly string[] Names =
    {
        "Balam", "Itzel", "Ixchel", "Kinich", "Pakal", "Chaac", "Itzamna", "Yaxkin",
        "Nicte", "Zazil", "Ahau", "Kukulkan", "Hunahpu", "Kan", "Tohil", "Ixkin"
    };

    /// <summary>
    /// The name for this account, with the title in the current language
    /// </summary>
    public static string For(string playFabId)
    {
        uint hash = StableHash(playFabId ?? "");
        int title = (int)(hash % TitleCount);
        int name = (int)(hash / TitleCount % (uint)Names.Length);
        return $"{Title(title)} {Names[name]}";
    }

    // English copies of mayan_title_N: the name is saved on the server for good, so it must
    // never end up as a raw key because localization wasn't loaded yet.
    private static readonly string[] EnglishTitles =
    {
        "Jade", "Jaguar", "Serpent", "Sun", "Moon", "Obsidian",
        "Quetzal", "Maize", "Eagle", "Star", "Temple", "Cacao"
    };

    private static string Title(int index)
    {
        string key = $"mayan_title_{index}";
        if (LocalizationManager.Instance != null)
        {
            string localized = LocalizationManager.Get(key);
            if (!string.IsNullOrEmpty(localized) && localized != key) return localized;
        }
        return EnglishTitles[index];
    }

    /// <summary>
    /// True for names nobody chose: empty, the old "Player_XXXXXX" default, or a raw ID
    /// </summary>
    public static bool IsPlaceholder(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return true;
        if (displayName.StartsWith("Player_")) return true;
        return displayName.Length >= 16 && IsIdLike(displayName);
    }

    // PlayFab and entity IDs are 16 hex characters
    private static bool IsIdLike(string s)
    {
        foreach (char c in s)
        {
            bool hex = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
            if (!hex && c != '-') return false;
        }
        return true;
    }

    // FNV-1a: string.GetHashCode isn't guaranteed to match between runtimes or app versions
    private static uint StableHash(string s)
    {
        uint hash = 2166136261;
        foreach (char c in s)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }
}
