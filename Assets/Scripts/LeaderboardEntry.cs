using UnityEngine;

/// <summary>
/// Data class representing a single entry in a leaderboard
/// </summary>
[System.Serializable]
public class LeaderboardEntry
{
    public int position;        // 1-based position (1 = first place)
    public string playerName;   // Display name or PlayFabId
    public int score;           // Player's score
    public bool isCurrentPlayer; // True if this is the current player's entry

    public LeaderboardEntry(int position, string playerName, int score, bool isCurrentPlayer = false)
    {
        this.position = position;
        this.playerName = playerName;
        this.score = score;
        this.isCurrentPlayer = isCurrentPlayer;
    }

    /// <summary>
    /// Format the player name for display (truncate if too long)
    /// </summary>
    public string GetDisplayName(int maxLength = 20)
    {
        if (string.IsNullOrEmpty(playerName))
            return "Anonymous";

        if (playerName.Length <= maxLength)
            return playerName;

        return playerName.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Format the position with appropriate suffix (1st, 2nd, 3rd, etc.)
    /// </summary>
    public string GetPositionText()
    {
        string suffix = "th";

        if (position % 100 >= 11 && position % 100 <= 13)
        {
            suffix = "th";
        }
        else
        {
            switch (position % 10)
            {
                case 1: suffix = "st"; break;
                case 2: suffix = "nd"; break;
                case 3: suffix = "rd"; break;
            }
        }

        return $"{position}";
    }
}

