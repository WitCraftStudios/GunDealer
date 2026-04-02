using UnityEngine;

[CreateAssetMenu(fileName = "DifficultyPreset", menuName = "GunBlackMarket/DifficultyPreset")]
public class DifficultyPreset : ScriptableObject
{
    [Header("Economy")]
    [Min(0)] public int startingCash = 350;
    [Min(1)] public int minimumOrderPayout = 25;
    [Range(0f, 3f)] public float orderPayoutMultiplier = 1f;
    [Min(0)] public int cashWinGoal = 5000;

    [Header("Heat Thresholds")]
    [Range(0, 100)] public int heatWarningThreshold = 60;
    [Range(0, 100)] public int heatRaidThreshold = 90;
    [Min(0)] public int inspectionBribeCost = 250;

    [Header("Campaign")]
    [Min(1)] public int maxShutdowns = 3;
    [Min(1)] public int jobsPerDay = 3;
}
