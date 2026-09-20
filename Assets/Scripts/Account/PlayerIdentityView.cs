using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows who the player is playing as: their leaderboard name and whether their progress
/// is tied to Google Play or only to this device. The optional button lets a player whose
/// Google sign-in failed at launch try again, which moves their progress to Google.
///
/// Used on the main menu header (name only) and in the Settings panel (name, status and
/// button). Refreshes live whenever PlayFabManager reports an account change.
/// </summary>
public class PlayerIdentityView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [Tooltip("Optional: 'Signed in with Google Play' / 'Saved on this device only'")]
    [SerializeField] private TextMeshProUGUI statusText;
    [Tooltip("Optional: shown only while the player can still sign in with Google Play")]
    [SerializeField] private Button googleSignInButton;
    [Tooltip("Localization key for the name line; {0} is the display name")]
    [SerializeField] private string nameFormatKey = "player_identity_format";

    private PlayFabManager playFabManager;
    private LocalizationManager localization;

    private void OnEnable()
    {
        Bind();
        Refresh();
    }

    private void Start()
    {
        // PlayFabManager registers in its Awake, which can run after this OnEnable
        Bind();
        Refresh();

        if (googleSignInButton != null)
        {
            googleSignInButton.onClick.AddListener(OnGoogleSignInClicked);
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        if (googleSignInButton != null)
        {
            googleSignInButton.onClick.RemoveListener(OnGoogleSignInClicked);
        }
    }

    private void Bind()
    {
        if (playFabManager == null)
        {
            playFabManager = DependencyRegistry.Find<PlayFabManager>();
            if (playFabManager != null) playFabManager.OnAccountChanged += Refresh;
        }

        if (localization == null)
        {
            localization = LocalizationManager.Instance;
            if (localization != null) localization.OnLanguageChanged += Refresh;
        }
    }

    private void Unbind()
    {
        if (playFabManager != null) playFabManager.OnAccountChanged -= Refresh;
        if (localization != null) localization.OnLanguageChanged -= Refresh;
        playFabManager = null;
        localization = null;
    }

    private void Refresh()
    {
        bool loggedIn = playFabManager != null && playFabManager.IsLoggedIn;
        string name = loggedIn ? playFabManager.CurrentDisplayName : "";

        if (nameText != null)
        {
            if (!string.IsNullOrEmpty(name))
            {
                nameText.text = LocalizationManager.Get(nameFormatKey, name);
            }
            else if (playFabManager != null && (playFabManager.IsLoggingIn || loggedIn))
            {
                // Logged in but the temple name is still being saved counts as signing in too
                nameText.text = LocalizationManager.Get("player_identity_signing_in");
            }
            else
            {
                nameText.text = LocalizationManager.Get("player_identity_offline");
            }
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(loggedIn);
            if (loggedIn)
            {
                statusText.text = LocalizationManager.Get(playFabManager.UsedGoogleLogin
                    ? "player_identity_google"
                    : "player_identity_device");
            }
        }

        if (googleSignInButton != null)
        {
            googleSignInButton.gameObject.SetActive(playFabManager != null && playFabManager.CanSignInWithGoogle);
        }
    }

    private void OnGoogleSignInClicked()
    {
        if (playFabManager != null) playFabManager.SignInWithGooglePlay();
    }
}
