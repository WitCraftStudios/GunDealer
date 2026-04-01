using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance;

    [Header("Slots")]
    public PartSlot gripSlot;
    public PartSlot triggerSlot;
    public PartSlot magSlot;
    public PartSlot bodySlot;

    // Removed UI Button reference in favor of physical button
    
    [Header("Final Gun")]
    public GameObject finalGunPrefab; // Simple Cube for now, Material purple
    public Transform assembledGunSpawnPoint;

    [Header("Player Ref")]
    public PlayerController playerController;
    public bool requireAcceptedOrder = true;

    public bool correctAssembly;
    public bool isAssembled;
    public bool canAssemble; // New flag for physical button

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate CraftingManager found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        Instance = this;
        RuntimeGameBootstrap.EnsureCoreSystems();
    }

    public void CheckAssembly()
    {
        bool wasReady = canAssemble;

        // specific logic: Just check if full
        canAssemble = gripSlot != null && triggerSlot != null && magSlot != null && bodySlot != null &&
                      gripSlot.placedPart != null && triggerSlot.placedPart != null &&
                      magSlot.placedPart != null && bodySlot.placedPart != null;

        if (canAssemble && !wasReady)
        {
            GameFeedback.Show("Assembly ready. Press the red button to build the gun.");
        }

        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
    }

    public void AssembleGun()
    {
        if (isAssembled || !canAssemble) return;

        if (finalGunPrefab == null)
        {
            GameFeedback.Error("The final gun prefab is missing from the crafting setup.");
            return;
        }

        if (requireAcceptedOrder && (OrderManager.Instance == null || !OrderManager.Instance.HasAcceptedOrder))
        {
            GameFeedback.Show("Accept an order before assembling a gun.", 1.8f);
            return;
        }

        GunOrder order = OrderManager.Instance != null ? OrderManager.Instance.currentOrder : null;
        if (order == null)
        {
            GameFeedback.Error("No current order is active.");
            return;
        }

        correctAssembly = (gripSlot.placedPart.type == order.grip) &&
                          (triggerSlot.placedPart.type == order.trigger) &&
                          (magSlot.placedPart.type == order.magazine) &&
                          (bodySlot.placedPart.type == order.body);

        Transform spawnTarget = assembledGunSpawnPoint != null ? assembledGunSpawnPoint : transform;
        GameObject gun = Instantiate(finalGunPrefab, spawnTarget.position + Vector3.up * 0.5f, spawnTarget.rotation);

        // Notify result
        if (correctAssembly) GameFeedback.Show("Gun assembled correctly. Get it into the case.");
        else GameFeedback.Warn("Gun assembled with the wrong parts. The payout will be reduced.");

        // Clear slots
        ClearSlot(gripSlot);
        ClearSlot(triggerSlot);
        ClearSlot(magSlot);
        ClearSlot(bodySlot);

        canAssemble = false;
        isAssembled = true;

        // Auto-pick up assembled gun
        if (PlayerInventory.Instance == null || !PlayerInventory.Instance.PickUpItem(gun))
        {
            GameFeedback.Show("The assembled gun is waiting on the bench.");
        }

        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
    }

    public string GetMissingSlotsSummary()
    {
        if (requireAcceptedOrder && (OrderManager.Instance == null || !OrderManager.Instance.HasAcceptedOrder))
        {
            return "Accept an order before assembling.";
        }

        string missing = string.Empty;

        if (gripSlot == null || gripSlot.placedPart == null) missing += "grip, ";
        if (triggerSlot == null || triggerSlot.placedPart == null) missing += "trigger, ";
        if (magSlot == null || magSlot.placedPart == null) missing += "magazine, ";
        if (bodySlot == null || bodySlot.placedPart == null) missing += "body, ";

        if (string.IsNullOrWhiteSpace(missing))
        {
            return "All parts are in place.";
        }

        missing = missing.TrimEnd(' ', ',');
        return $"Missing: {missing}.";
    }

    public string GetAssemblyStatusLine()
    {
        if (isAssembled)
        {
            return "Bench: Gun assembled";
        }

        int filledSlots = GetFilledSlotCount();
        return $"Bench: {filledSlots}/4 parts placed";
    }

    int GetFilledSlotCount()
    {
        int filledSlots = 0;
        if (gripSlot != null && gripSlot.placedPart != null) filledSlots++;
        if (triggerSlot != null && triggerSlot.placedPart != null) filledSlots++;
        if (magSlot != null && magSlot.placedPart != null) filledSlots++;
        if (bodySlot != null && bodySlot.placedPart != null) filledSlots++;
        return filledSlots;
    }

    void ClearSlot(PartSlot slot)
    {
        if (slot != null && slot.placedPart != null)
        {
            Destroy(slot.placedPart.gameObject);
            slot.placedPart = null;
        }
    }
}
