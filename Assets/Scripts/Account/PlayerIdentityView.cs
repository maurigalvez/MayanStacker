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

    private void Update()
    {
        // Awake order across objects isn't fixed, so this view can come up before
        // LocalizationManager or PlayFabManager and would then sit on the raw key
        // ("player_identity_offline") while the sync screen already names the player.
        // Keep binding until both exist, and re-render whenever the sign-in state moves
        // without an event reaching us.
        if (playFabManager == null || localization == null) Bind();
        if (StateChanged()) Refresh();
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
        if (playFabManager == null && DependencyRegistry.TryFind(out playFabManager))
        {
            playFabManager.OnAccountChanged += Refresh;
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

    // What the view last rendered; Update re-renders when any of it moves.
    private bool shownLoggedIn;
    private bool shownLoggingIn;
    private bool shownHasManager;
    private string shownName;
    private bool rendered;

    private bool StateChanged()
    {
        if (!rendered) return true;
        bool hasManager = playFabManager != null;
        return hasManager != shownHasManager
            || (hasManager && (playFabManager.IsLoggedIn != shownLoggedIn
                               || playFabManager.IsLoggingIn != shownLoggingIn
                               || playFabManager.CurrentDisplayName != shownName));
    }

    private void Refresh()
    {
        // Without LocalizationManager every string would come out as its raw key
        if (LocalizationManager.Instance == null) return;

        bool loggedIn = playFabManager != null && playFabManager.IsLoggedIn;
        string name = loggedIn ? playFabManager.CurrentDisplayName : "";

        rendered = true;
        shownHasManager = playFabManager != null;
        shownLoggedIn = loggedIn;
        shownLoggingIn = playFabManager != null && playFabManager.IsLoggingIn;
        shownName = playFabManager != null ? playFabManager.CurrentDisplayName : null;

        if (nameText != null)
        {
            if (!string.IsNullOrEmpty(name))
            {
                nameText.text = LocalizationManager.Get(nameFormatKey, name);
            }
            else if (playFabManager == null || playFabManager.IsLoggingIn || loggedIn)
            {
                // Logged in but the temple name is still being saved counts as signing in too,
                // and so does PlayFabManager not having come up yet: that's not "offline".
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
