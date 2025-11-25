using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component for displaying a single leaderboard entry
/// Attach this to a prefab that will be instantiated for each leaderboard entry
/// </summary>
public class LeaderboardEntryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI positionText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Image backgroundImage;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color currentPlayerColor = new Color(0.3f, 0.5f, 0.8f, 0.9f);
    [SerializeField] private Color topThreeColor = new Color(0.8f, 0.6f, 0.2f, 0.9f);

    [Header("Text Colors")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color currentPlayerTextColor = Color.yellow;
    [SerializeField] private Color firstPlaceTextColor = new Color(1f, 0.84f, 0f); // Gold
    [SerializeField] private Color secondPlaceTextColor = new Color(0.75f, 0.75f, 0.75f); // Silver
    [SerializeField] private Color thirdPlaceTextColor = new Color(0.8f, 0.5f, 0.2f); // Bronze

    /// <summary>
    /// Set up the UI with leaderboard entry data
    /// </summary>
    public void SetData(LeaderboardEntry entry)
    {
        if (entry == null) return;

        // Set position
        if (positionText != null)
        {
            positionText.text = entry.GetPositionText();
        }

        // Set player name
        if (playerNameText != null)
        {
            playerNameText.text = entry.GetDisplayName();
        }

        // Set score
        if (scoreText != null)
        {
            scoreText.text = entry.score.ToString();
        }

        // Update visual style based on entry type
        UpdateVisualStyle(entry);
    }

    /// <summary>
    /// Update the visual style based on whether this is the current player or top 3
    /// </summary>
    private void UpdateVisualStyle(LeaderboardEntry entry)
    {
        Color bgColor = normalColor;
        Color textColor = normalTextColor;

        // Determine text color based on position (prioritize current player color)
        if (entry.isCurrentPlayer)
        {
            bgColor = currentPlayerColor;
            textColor = currentPlayerTextColor;
        }
        else if (entry.position == 1)
        {
            bgColor = topThreeColor;
            textColor = firstPlaceTextColor;
        }
        else if (entry.position == 2)
        {
            bgColor = topThreeColor;
            textColor = secondPlaceTextColor;
        }
        else if (entry.position == 3)
        {
            bgColor = topThreeColor;
            textColor = thirdPlaceTextColor;
        }

        // Apply background color
        if (backgroundImage != null)
        {
            backgroundImage.color = bgColor;
        }

        // Apply text color to all text elements
        if (positionText != null) positionText.color = textColor;
        if (playerNameText != null) playerNameText.color = textColor;
        if (scoreText != null) scoreText.color = textColor;
    }

    /// <summary>
    /// Clear the entry data
    /// </summary>
    public void Clear()
    {
        if (positionText != null) positionText.text = "";
        if (playerNameText != null) playerNameText.text = "";
        if (scoreText != null) scoreText.text = "";
        if (backgroundImage != null) backgroundImage.color = normalColor;
    }
}

