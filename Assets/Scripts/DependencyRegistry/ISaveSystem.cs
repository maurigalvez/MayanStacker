using System;
using System.Collections.Generic;

public interface ISaveSystem
{
    /// <summary>
    /// Saves data of any type to the specified key
    /// </summary>
    /// <typeparam name="T">Type of data to save</typeparam>
    /// <param name="key">Unique identifier for the saved data</param>
    /// <param name="data">Data to save</param>
    /// <returns>True if save was successful, false otherwise</returns>
    bool SaveData<T>(string key, T data);
    
    /// <summary>
    /// Loads data of the specified type from the given key
    /// </summary>
    /// <typeparam name="T">Type of data to load</typeparam>
    /// <param name="key">Unique identifier for the saved data</param>
    /// <param name="defaultValue">Default value to return if data doesn't exist</param>
    /// <returns>Loaded data or default value if not found</returns>
    T LoadData<T>(string key, T defaultValue = default(T));
    
    /// <summary>
    /// Checks if data exists for the given key
    /// </summary>
    /// <param name="key">Key to check</param>
    /// <returns>True if data exists, false otherwise</returns>
    bool HasData(string key);
    
    /// <summary>
    /// Deletes data for the given key
    /// </summary>
    /// <param name="key">Key to delete</param>
    /// <returns>True if deletion was successful, false otherwise</returns>
    bool DeleteData(string key);
    
    /// <summary>
    /// Deletes all saved data
    /// </summary>
    /// <returns>True if deletion was successful, false otherwise</returns>
    bool DeleteAllData();
    
    /// <summary>
    /// Gets the file path where data is stored
    /// </summary>
    /// <returns>File path as string</returns>
    string GetSaveFilePath();

    /// <summary>
    /// Gets all keys stored in the save system
    /// </summary>
    /// <returns>List of all keys</returns>
    List<string> GetAllKeys();
}
