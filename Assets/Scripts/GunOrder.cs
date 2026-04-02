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

    [Header("Required Parts")]
    [Tooltip("Uncheck to make this slot optional for this order (e.g. pistols don't need a magazine or body).")]
    public bool requiresGrip    = true;
    public bool requiresTrigger = true;
    public bool requiresMag     = true;
    public bool requiresBody    = true;
    public PartType grip;
    public PartType trigger;
    public PartType magazine;
    public PartType body;

    [Header("Timing & Risk")]
    public float timeLimit = 60f;
    [Range(0f, 3f)] public float riskBonusMultiplier = 1f;
    [Min(0)] public int heatOnAccept = 2;
    [Min(0)] public int heatOnMistake = 12;
    [Min(0)] public int heatOnLateDelivery = 8;
    [Min(0)] public int heatOnCleanCompletion = 3;

    [TextArea]
    public string orderNotes;

    /// <summary>Returns the number of slots this order actually requires.</summary>
    public int RequiredSlotCount =>
        (requiresGrip ? 1 : 0) + (requiresTrigger ? 1 : 0) +
        (requiresMag  ? 1 : 0) + (requiresBody    ? 1 : 0);
}

public enum PartType { None, GripA, GripB, Trigger1, Trigger2, MagShort, MagLong, BodyAK, BodyAR, Assembled }
public enum OrderRisk { Low, Medium, High }
