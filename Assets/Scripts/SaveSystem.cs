using System;
using System.IO;
using UnityEngine;

/// <summary>
/// JSON file-based save system. Replaces PlayerPrefs.
/// All data is written to Application.persistentDataPath/GunDealer/.
/// Falls back gracefully to supplied defaults if files are missing or corrupt.
/// </summary>
public static class SaveSystem
{
    static string SaveDirectory => Path.Combine(Application.persistentDataPath, "GunDealer");

    // -------------------------------------------------------------------------
    // Generic typed save / load
    // -------------------------------------------------------------------------

    public static void Save<T>(string key, T data)
    {
        try
        {
            EnsureDirectoryExists();
            string json = JsonUtility.ToJson(new Wrapper<T> { value = data }, prettyPrint: false);
            File.WriteAllText(FilePath(key), json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Failed to save '{key}': {ex.Message}");
        }
    }

    public static T Load<T>(string key, T defaultValue = default)
    {
        string path = FilePath(key);
        if (!File.Exists(path)) return defaultValue;

        try
        {
            string json = File.ReadAllText(path);
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
            return wrapper != null ? wrapper.value : defaultValue;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveSystem] Corrupt save for '{key}', using default. ({ex.Message})");
            return defaultValue;
        }
    }

    public static void Delete(string key)
    {
        string path = FilePath(key);
        if (File.Exists(path))
        {
            try { File.Delete(path); }
            catch (Exception ex) { Debug.LogWarning($"[SaveSystem] Could not delete '{key}': {ex.Message}"); }
        }
    }

    public static void DeleteAll()
    {
        if (!Directory.Exists(SaveDirectory)) return;
        try
        {
            Directory.Delete(SaveDirectory, recursive: true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveSystem] Could not delete save directory: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Primitive convenience wrappers
    // -------------------------------------------------------------------------

    public static void SaveInt(string key, int value) => Save(key, value);
    public static int LoadInt(string key, int defaultValue = 0) => Load(key, defaultValue);

    public static void SaveBool(string key, bool value) => Save(key, value);
    public static bool LoadBool(string key, bool defaultValue = false) => Load(key, defaultValue);

    public static void SaveFloat(string key, float value) => Save(key, value);
    public static float LoadFloat(string key, float defaultValue = 0f) => Load(key, defaultValue);

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    static string FilePath(string key) => Path.Combine(SaveDirectory, SanitizeKey(key) + ".json");

    static string SanitizeKey(string key)
    {
        // Replace characters that are invalid in file names
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            key = key.Replace(c, '_');
        }
        return key;
    }

    static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(SaveDirectory))
        {
            Directory.CreateDirectory(SaveDirectory);
        }
    }

    // JsonUtility can't serialize primitives directly — wrap them.
    [Serializable]
    class Wrapper<T>
    {
        public T value;
    }
}
