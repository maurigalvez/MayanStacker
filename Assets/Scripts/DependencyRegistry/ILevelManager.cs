using UnityEngine;

public interface ILevelManager
{
    void LoadLevel(int levelIndex);
    void NextLevel();
    void RestartLevel();
    // Add other level management methods as needed
} 