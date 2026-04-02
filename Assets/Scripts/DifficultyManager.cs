using UnityEngine;

/// <summary>
/// Applies a DifficultyPreset to all game managers before they initialize.
/// Must run before RewardManager, HeatManager, DayManager, CampaignManager.
/// RuntimeGameBootstrap calls DifficultyManager.Instance first.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    private static DifficultyManager instance;
    const string DifficultyKey = "Settings.DifficultyPresetName";

    [Header("Presets — drag Easy/Normal/Hard assets here")]
    public DifficultyPreset[] presets;
    public DifficultyPreset defaultPreset;

    private DifficultyPreset activePreset;

    public static bool HasLiveInstance => instance != null || FindFirstObjectByType<DifficultyManager>() != null;

    public static DifficultyManager Instance
    {
        get
        {
            if (instance == null) instance = FindFirstObjectByType<DifficultyManager>();
            if (instance == null)
            {
                GameObject go = new GameObject("DifficultyManager");
                instance = go.AddComponent<DifficultyManager>();
            }
            return instance;
        }
    }

    public DifficultyPreset ActivePreset => activePreset;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;

        // Pick preset by saved name, or use the default
        string savedName = SaveSystem.Load(DifficultyKey, defaultPreset != null ? defaultPreset.name : "Normal");
        activePreset = FindPresetByName(savedName) ?? defaultPreset;

        if (activePreset == null && presets != null && presets.Length > 0)
            activePreset = presets[0];

        if (activePreset == null)
        {
            // No preset configured — create a runtime fallback with sensible defaults
            activePreset = ScriptableObject.CreateInstance<DifficultyPreset>();
            activePreset.name = "Default (runtime)";
        }
    }

    /// <summary>Call this to switch difficulty (takes effect on next campaign restart).</summary>
    public void SetDifficulty(string presetName)
    {
        DifficultyPreset found = FindPresetByName(presetName);
        if (found == null)
        {
            GameFeedback.Warn($"Difficulty preset '{presetName}' not found.");
            return;
        }
        activePreset = found;
        SaveSystem.Save(DifficultyKey, presetName);
        GameFeedback.Show($"Difficulty set to {presetName}. Restart the campaign for changes to take effect.");
    }

    DifficultyPreset FindPresetByName(string presetName)
    {
        if (presets == null) return null;
        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i] != null && presets[i].name == presetName)
                return presets[i];
        }
        return null;
    }
}
