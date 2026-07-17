using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight localization system for TamalStacker.
/// Loads flat key-value JSON files from Resources/Localization/.
/// Register via DependencyRegistry, access strings via static Get() methods.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    private const string LANGUAGE_KEY = "Language";
    private const string DEFAULT_LOCALE = "en";

    private static LocalizationManager instance;

    private Dictionary<string, Dictionary<string, string>> locales = new Dictionary<string, Dictionary<string, string>>();
    private string currentLocale = DEFAULT_LOCALE;

    // Achievement overlay: achievement id -> { title, description }
    private Dictionary<string, AchievementLocaleEntry> achievementOverlay;

    // Level overlay: level number -> { levelName, location, levelDescription }
    private Dictionary<int, LevelLocaleEntry> levelOverlay;

    public event Action OnLanguageChanged;

    public string CurrentLocale => currentLocale;

    public static LocalizationManager Instance => instance;

    private void Awake()
    {
        // Singleton: if an instance already exists (persisted from a previous scene), destroy this duplicate
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        DependencyRegistry.Register<LocalizationManager>(this);

        // Load saved language or detect device language
        currentLocale = PlayerPrefs.GetString(LANGUAGE_KEY, DetectDeviceLocale());

        LoadLocale("en");
        LoadLocale("es-419");
        LoadLocale("zh-Hans");
        LoadLocale("zh-Hant");
        LoadLocale("pt-BR");
        LoadLocale("ja");
        LoadOverlays();
    }

    private string DetectDeviceLocale()
    {
        var lang = Application.systemLanguage;
        switch (lang)
        {
            case SystemLanguage.Spanish:
                return "es-419";
            case SystemLanguage.ChineseSimplified:
                return "zh-Hans";
            case SystemLanguage.ChineseTraditional:
                return "zh-Hant";
            case SystemLanguage.Chinese:
                return "zh-Hans";
            case SystemLanguage.Portuguese:
                return "pt-BR";
            case SystemLanguage.Japanese:
                return "ja";
            default:
                return DEFAULT_LOCALE;
        }
    }

    private void LoadLocale(string localeCode)
    {
        TextAsset asset = Resources.Load<TextAsset>($"Localization/{localeCode}");
        if (asset == null)
        {
            Debug.LogWarning($"LocalizationManager: Locale file not found: Localization/{localeCode}");
            return;
        }

        var wrapper = JsonUtility.FromJson<LocaleFile>(asset.text);
        if (wrapper == null || wrapper.entries == null)
        {
            Debug.LogError($"LocalizationManager: Failed to parse locale file: {localeCode}");
            return;
        }

        var dict = new Dictionary<string, string>();
        foreach (var entry in wrapper.entries)
        {
            dict[entry.key] = entry.value;
        }
        locales[localeCode] = dict;

        Debug.Log($"LocalizationManager: Loaded {dict.Count} strings for locale '{localeCode}'");
    }

    private void LoadOverlays()
    {
        // Load achievement overlay for current non-English locale
        achievementOverlay = new Dictionary<string, AchievementLocaleEntry>();
        levelOverlay = new Dictionary<int, LevelLocaleEntry>();

        if (currentLocale == "en") return;

        LoadAchievementOverlay(currentLocale);
        LoadLevelOverlay(currentLocale);
    }

    private void LoadAchievementOverlay(string localeCode)
    {
        achievementOverlay = new Dictionary<string, AchievementLocaleEntry>();

        TextAsset asset = Resources.Load<TextAsset>($"Localization/achievements_{localeCode}");
        if (asset == null) return;

        var wrapper = JsonUtility.FromJson<AchievementLocaleFile>(asset.text);
        if (wrapper?.achievements == null) return;

        foreach (var entry in wrapper.achievements)
        {
            achievementOverlay[entry.id] = entry;
        }

        Debug.Log($"LocalizationManager: Loaded {achievementOverlay.Count} achievement translations for '{localeCode}'");
    }

    private void LoadLevelOverlay(string localeCode)
    {
        levelOverlay = new Dictionary<int, LevelLocaleEntry>();

        TextAsset asset = Resources.Load<TextAsset>($"Localization/levels_{localeCode}");
        if (asset == null) return;

        var wrapper = JsonUtility.FromJson<LevelLocaleFile>(asset.text);
        if (wrapper?.levels == null) return;

        foreach (var entry in wrapper.levels)
        {
            levelOverlay[entry.levelNumber] = entry;
        }

        Debug.Log($"LocalizationManager: Loaded {levelOverlay.Count} level translations for '{localeCode}'");
    }

    /// <summary>
    /// Set the active language and fire the change event.
    /// </summary>
    public void SetLanguage(string localeCode)
    {
        if (currentLocale == localeCode) return;
        if (!locales.ContainsKey(localeCode))
        {
            Debug.LogWarning($"LocalizationManager: Locale '{localeCode}' not loaded");
            return;
        }

        currentLocale = localeCode;
        PlayerPrefs.SetString(LANGUAGE_KEY, localeCode);
        PlayerPrefs.Save();

        // Reload overlays for new locale
        LoadOverlays();

        Debug.Log($"LocalizationManager: Language changed to '{localeCode}'");
        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Get a localized string by key for the current language.
    /// Falls back to English if key is missing in current locale.
    /// </summary>
    public static string Get(string key)
    {
        if (instance == null)
        {
            Debug.LogWarning($"LocalizationManager: Instance not ready, returning key '{key}'");
            return key;
        }

        // Try current locale
        if (instance.locales.TryGetValue(instance.currentLocale, out var currentDict))
        {
            if (currentDict.TryGetValue(key, out var value))
                return value;
        }

        // Fallback to English
        if (instance.currentLocale != "en" && instance.locales.TryGetValue("en", out var enDict))
        {
            if (enDict.TryGetValue(key, out var value))
                return value;
        }

        Debug.LogWarning($"LocalizationManager: Missing key '{key}'");
        return key;
    }

    /// <summary>
    /// Get a localized string with format arguments.
    /// </summary>
    public static string Get(string key, params object[] args)
    {
        string format = Get(key);
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"LocalizationManager: Format error for key '{key}' with {args.Length} args");
            return format;
        }
    }

    /// <summary>
    /// Get localized achievement title. Returns original if no overlay exists.
    /// </summary>
    public static string GetAchievementTitle(string achievementId, string fallback)
    {
        if (instance == null || instance.currentLocale == "en")
            return fallback;

        if (instance.achievementOverlay.TryGetValue(achievementId, out var entry))
            return entry.title;

        return fallback;
    }

    /// <summary>
    /// Get localized achievement description. Returns original if no overlay exists.
    /// </summary>
    public static string GetAchievementDescription(string achievementId, string fallback)
    {
        if (instance == null || instance.currentLocale == "en")
            return fallback;

        if (instance.achievementOverlay.TryGetValue(achievementId, out var entry))
            return entry.description;

        return fallback;
    }

    /// <summary>
    /// Get localized level name. Returns original if no overlay exists.
    /// </summary>
    public static string GetLevelName(LevelData level)
    {
        if (instance == null || level == null || instance.currentLocale == "en")
            return level?.levelName ?? "";

        if (instance.levelOverlay.TryGetValue(level.levelNumber, out var entry))
            return entry.levelName;

        return level.levelName;
    }

    /// <summary>
    /// Get localized level location. Returns original if no overlay exists.
    /// </summary>
    public static string GetLevelLocation(LevelData level)
    {
        if (instance == null || level == null || instance.currentLocale == "en")
            return level?.location ?? "";

        if (instance.levelOverlay.TryGetValue(level.levelNumber, out var entry))
            return entry.location;

        return level.location;
    }

    /// <summary>
    /// Get localized level description. Returns original if no overlay exists.
    /// </summary>
    public static string GetLevelDescription(LevelData level)
    {
        if (instance == null || level == null || instance.currentLocale == "en")
            return level?.levelDescription ?? "";

        if (instance.levelOverlay.TryGetValue(level.levelNumber, out var entry))
            return entry.levelDescription;

        return level.levelDescription;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
        DependencyRegistry.Unregister<LocalizationManager>(this);
    }

    // JSON serialization types (Unity's JsonUtility requires wrapper classes)

    [Serializable]
    private class LocaleFile
    {
        public LocaleEntry[] entries;
    }

    [Serializable]
    private class LocaleEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    private class AchievementLocaleFile
    {
        public AchievementLocaleEntry[] achievements;
    }

    [Serializable]
    private class AchievementLocaleEntry
    {
        public string id;
        public string title;
        public string description;
    }

    [Serializable]
    private class LevelLocaleFile
    {
        public LevelLocaleEntry[] levels;
    }

    [Serializable]
    private class LevelLocaleEntry
    {
        public int levelNumber;
        public string levelName;
        public string location;
        public string levelDescription;
    }
}
