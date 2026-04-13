using TMPro;
using UnityEngine;

/// <summary>
/// Attach to any GameObject with a TMP_Text component to automatically
/// set and refresh localized text based on a key.
/// Useful for static labels like button text and panel titles.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string localizationKey;

    private TMP_Text textComponent;

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        UpdateText();

        var locManager = LocalizationManager.Instance;
        if (locManager != null)
        {
            locManager.OnLanguageChanged += UpdateText;
        }
    }

    private void OnDestroy()
    {
        var locManager = LocalizationManager.Instance;
        if (locManager != null)
        {
            locManager.OnLanguageChanged -= UpdateText;
        }
    }

    private void UpdateText()
    {
        if (textComponent != null && !string.IsNullOrEmpty(localizationKey))
        {
            textComponent.text = LocalizationManager.Get(localizationKey);
        }
    }
}
