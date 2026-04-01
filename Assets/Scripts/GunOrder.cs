using UnityEngine;

[CreateAssetMenu(fileName = "GunOrder", menuName = "GunBlackMarket/GunOrder")]
public class GunOrder : ScriptableObject
{
    public string buyerName;
    public string gunName;
    public int price;
    public OrderRisk riskLevel = OrderRisk.Low;
    [Min(1)] public int unlockDay = 1;
    [Min(1)] public int selectionWeight = 1;
    public PartType grip;
    public PartType trigger;
    public PartType magazine;
    public PartType body;
    public float timeLimit = 60f; // Seconds given to complete this order
    [Range(0f, 3f)] public float riskBonusMultiplier = 1f;
    [Min(0)] public int heatOnAccept = 2;
    [Min(0)] public int heatOnMistake = 12;
    [Min(0)] public int heatOnLateDelivery = 8;
    [Min(0)] public int heatOnCleanCompletion = 3;

    [TextArea]
    public string orderNotes;
}

public enum PartType { None, GripA, GripB, Trigger1, Trigger2, MagShort, MagLong, BodyAK, BodyAR, Assembled }
public enum OrderRisk { Low, Medium, High }
