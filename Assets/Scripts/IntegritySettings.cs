using UnityEngine;

/// <summary>
/// ScriptableObject to store Google Play Integrity API settings.
/// Create an instance via: Assets > Create > TamalStacker > Integrity Settings
/// </summary>
[CreateAssetMenu(fileName = "IntegritySettings", menuName = "TamalStacker/Integrity Settings")]
public class IntegritySettings : ScriptableObject
{
    [Header("Integrity API Configuration")]
    [Tooltip("Enable or disable integrity checks globally")]
    public bool enableIntegrityChecks = true;

    [Tooltip("Google Cloud Project Number (optional for apps distributed via Google Play)")]
    [SerializeField] private long cloudProjectNumber = 0;

    [Header("Check Configuration")]
    [Tooltip("Perform integrity check on app startup")]
    public bool checkOnStartup = true;

    [Tooltip("Perform integrity check before each game session")]
    public bool checkOnGameSessionStart = true;

    [Tooltip("Perform integrity check before leaderboard submissions")]
    public bool checkBeforeLeaderboardSubmission = true;

    [Header("Debug Settings")]
    [Tooltip("Enable detailed logging of integrity check results")]
    public bool verboseLogging = true;

    [Tooltip("Allow gameplay even if integrity checks fail (recommended for testing)")]
    public bool allowGameplayOnFailure = true;

    [Header("Information")]
    [TextArea(3, 10)]
    [SerializeField] private string setupInstructions = 
        "1. Link your app to Google Play Console\n" +
        "2. Enable Play Integrity API in Play Console\n" +
        "3. (Optional) Set Cloud Project Number if distributing outside Play Store\n" +
        "4. Build and test on Android device with Google Play Services\n\n" +
        "See GOOGLE_PLAY_INTEGRITY_SETUP.md for detailed instructions.";

    /// <summary>
    /// Get the cloud project number (0 means not set, which is fine for Play Store apps)
    /// </summary>
    public long CloudProjectNumber => cloudProjectNumber;

    /// <summary>
    /// Set the cloud project number
    /// </summary>
    public void SetCloudProjectNumber(long projectNumber)
    {
        cloudProjectNumber = projectNumber;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}

