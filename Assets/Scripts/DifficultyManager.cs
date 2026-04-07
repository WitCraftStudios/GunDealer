using System;
using System.Collections.Generic;
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
    public string resourcePresetsPath = "DifficultyPresets";

    private DifficultyPreset activePreset;
    DifficultyPreset[] cachedPresets;

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

        RefreshPresetCache();

        // Pick preset by saved name, or use the default
        string fallbackPresetName = defaultPreset != null ? defaultPreset.name : "Normal";
        string savedName = SaveSystem.Load(DifficultyKey, fallbackPresetName);
        activePreset = FindPresetByName(savedName) ?? defaultPreset ?? FindPresetByName("Normal");

        if (activePreset == null && cachedPresets != null && cachedPresets.Length > 0)
            activePreset = cachedPresets[0];

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

    void RefreshPresetCache()
    {
        List<DifficultyPreset> mergedPresets = new List<DifficultyPreset>();
        HashSet<DifficultyPreset> seenPresets = new HashSet<DifficultyPreset>();
        AddPresets(presets, mergedPresets, seenPresets);

        if (!string.IsNullOrWhiteSpace(resourcePresetsPath))
        {
            DifficultyPreset[] resourcePresets = Resources.LoadAll<DifficultyPreset>(resourcePresetsPath);
            AddPresets(resourcePresets, mergedPresets, seenPresets);
        }

        cachedPresets = mergedPresets.ToArray();

        if (defaultPreset == null)
        {
            defaultPreset = FindPresetByNameInternal("Normal", cachedPresets);
        }
    }

    void AddPresets(DifficultyPreset[] sourcePresets, List<DifficultyPreset> destination, HashSet<DifficultyPreset> seenPresets)
    {
        if (sourcePresets == null) return;
        for (int i = 0; i < sourcePresets.Length; i++)
        {
            DifficultyPreset preset = sourcePresets[i];
            if (preset == null || seenPresets.Contains(preset)) continue;
            seenPresets.Add(preset);
            destination.Add(preset);
        }
    }

    DifficultyPreset FindPresetByName(string presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName)) return null;
        if (cachedPresets == null || cachedPresets.Length == 0) RefreshPresetCache();
        return FindPresetByNameInternal(presetName, cachedPresets);
    }

    DifficultyPreset FindPresetByNameInternal(string presetName, DifficultyPreset[] searchSpace)
    {
        if (searchSpace == null) return null;
        for (int i = 0; i < searchSpace.Length; i++)
        {
            DifficultyPreset preset = searchSpace[i];
            if (preset != null && string.Equals(preset.name, presetName, StringComparison.OrdinalIgnoreCase))
                return preset;
        }
        return null;
    }
}
