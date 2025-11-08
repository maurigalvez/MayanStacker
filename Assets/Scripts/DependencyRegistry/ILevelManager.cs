using System.Collections.Generic;
using UnityEngine;

public interface ILevelManager
{
    void LoadLevel(int levelIndex);
    void NextLevel();
    void RestartLevel();
    
    // Level data access
    List<LevelData> GetAllLevels();
    int GetLevelStars(int levelNumber);
    int GetLevelHighScore(int levelNumber);
    bool IsLevelUnlocked(int levelNumber);
    
    // Properties
    LevelData CurrentLevel { get; }
    int CurrentLevelIndex { get; }
    int TotalLevels { get; }
    bool IsDemoVersion { get; }
    int DemoMaxLevel { get; }
} 