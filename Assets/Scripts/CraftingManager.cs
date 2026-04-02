using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance;

    [Header("Slots")]
    public PartSlot gripSlot;
    public PartSlot triggerSlot;
    public PartSlot magSlot;
    public PartSlot bodySlot;

    [Header("Final Gun")]
    public GameObject finalGunPrefab;
    public Transform assembledGunSpawnPoint;

    [Header("Player Ref")]
    public PlayerController playerController;
    public bool requireAcceptedOrder = true;

    public bool correctAssembly;
    public bool isAssembled;
    public bool canAssemble;

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

    // -------------------------------------------------------------------------
    // Slot requirement helpers (driven by the active order)
    // -------------------------------------------------------------------------

    GunOrder ActiveOrder => OrderManager.Instance != null ? OrderManager.Instance.currentOrder : null;

    bool SlotRequired(PartSlot slot, bool orderFlag)
    {
        if (slot == null) return false;
        // If no order is active yet fall back to treating all slots as required
        GunOrder order = ActiveOrder;
        return order == null ? true : orderFlag;
    }

    bool GripRequired    => SlotRequired(gripSlot,    ActiveOrder?.requiresGrip    ?? true);
    bool TriggerRequired => SlotRequired(triggerSlot, ActiveOrder?.requiresTrigger ?? true);
    bool MagRequired     => SlotRequired(magSlot,     ActiveOrder?.requiresMag     ?? true);
    bool BodyRequired    => SlotRequired(bodySlot,    ActiveOrder?.requiresBody    ?? true);

    bool SlotFilled(PartSlot slot, bool required) => !required || (slot != null && slot.placedPart != null);

    // -------------------------------------------------------------------------
    // Assembly check
    // -------------------------------------------------------------------------

    public void CheckAssembly()
    {
        bool wasReady = canAssemble;

        canAssemble =
            SlotFilled(gripSlot,    GripRequired)    &&
            SlotFilled(triggerSlot, TriggerRequired) &&
            SlotFilled(magSlot,     MagRequired)     &&
            SlotFilled(bodySlot,    BodyRequired);

        if (canAssemble && !wasReady)
        {
            GameFeedback.Show("Assembly ready. Press the red button to build the gun.");
        }

        // Mismatch warning: if all required slots are filled but parts don't match the order
        if (canAssemble && HasMismatch())
        {
            GameFeedback.Warn("⚠ Wrong parts on bench — payout will be cut if assembled.", 3f);
        }

        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
    }

    /// <summary>Returns true when at least one placed required part doesn't match the order recipe.</summary>
    public bool HasMismatch()
    {
        GunOrder order = ActiveOrder;
        if (order == null) return false;

        if (GripRequired    && gripSlot?.placedPart != null    && gripSlot.placedPart.type    != order.grip)    return true;
        if (TriggerRequired && triggerSlot?.placedPart != null && triggerSlot.placedPart.type != order.trigger) return true;
        if (MagRequired     && magSlot?.placedPart != null     && magSlot.placedPart.type     != order.magazine) return true;
        if (BodyRequired    && bodySlot?.placedPart != null    && bodySlot.placedPart.type    != order.body)    return true;
        return false;
    }

    // -------------------------------------------------------------------------
    // Assemble
    // -------------------------------------------------------------------------

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

        GunOrder order = ActiveOrder;
        if (order == null)
        {
            GameFeedback.Error("No current order is active.");
            return;
        }

        // Validate each required slot against the order
        correctAssembly =
            (!GripRequired    || (gripSlot?.placedPart != null    && gripSlot.placedPart.type    == order.grip))    &&
            (!TriggerRequired || (triggerSlot?.placedPart != null && triggerSlot.placedPart.type == order.trigger)) &&
            (!MagRequired     || (magSlot?.placedPart != null     && magSlot.placedPart.type     == order.magazine)) &&
            (!BodyRequired    || (bodySlot?.placedPart != null    && bodySlot.placedPart.type    == order.body));

        Transform spawnTarget = assembledGunSpawnPoint != null ? assembledGunSpawnPoint : transform;
        GameObject gun = Instantiate(finalGunPrefab, spawnTarget.position + Vector3.up * 0.5f, spawnTarget.rotation);
        GunWeapon gunWeapon = gun.GetComponent<GunWeapon>();
        if (gunWeapon == null) gunWeapon = gun.AddComponent<GunWeapon>();
        gunWeapon.Configure(playerController != null ? playerController : FindFirstObjectByType<PlayerController>(), correctAssembly, order);

        if (correctAssembly) GameFeedback.Show("Gun assembled correctly. Get it into the case.");
        else GameFeedback.Warn("Gun assembled with the wrong parts. The payout will be reduced.");

        // Clear only the slots that were required (optional slots stay empty)
        if (GripRequired)    ClearSlot(gripSlot);
        if (TriggerRequired) ClearSlot(triggerSlot);
        if (MagRequired)     ClearSlot(magSlot);
        if (BodyRequired)    ClearSlot(bodySlot);

        canAssemble = false;
        isAssembled = true;

        if (PlayerInventory.Instance == null || !PlayerInventory.Instance.PickUpItem(gun))
        {
            GameFeedback.Show("The assembled gun is waiting on the bench.");
        }

        // Tutorial hook
        TutorialManager.FireGunAssembled();

        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
    }

    // -------------------------------------------------------------------------
    // HUD helpers
    // -------------------------------------------------------------------------

    public string GetMissingSlotsSummary()
    {
        if (requireAcceptedOrder && (OrderManager.Instance == null || !OrderManager.Instance.HasAcceptedOrder))
            return "Accept an order before assembling.";

        string missing = string.Empty;
        if (GripRequired    && (gripSlot == null    || gripSlot.placedPart == null))    missing += "grip, ";
        if (TriggerRequired && (triggerSlot == null || triggerSlot.placedPart == null)) missing += "trigger, ";
        if (MagRequired     && (magSlot == null     || magSlot.placedPart == null))     missing += "magazine, ";
        if (BodyRequired    && (bodySlot == null    || bodySlot.placedPart == null))    missing += "body, ";

        if (string.IsNullOrWhiteSpace(missing)) return "All parts are in place.";

        return $"Missing: {missing.TrimEnd(' ', ',')}.";
    }

    public string GetAssemblyStatusLine()
    {
        if (isAssembled) return "Bench: Gun assembled";

        GunOrder order = ActiveOrder;
        int totalRequired = order != null ? order.RequiredSlotCount : 4;
        return $"Bench: {GetFilledSlotCount()}/{totalRequired} parts placed";
    }

    int GetFilledSlotCount()
    {
        int count = 0;
        if (GripRequired    && gripSlot != null    && gripSlot.placedPart != null)    count++;
        if (TriggerRequired && triggerSlot != null && triggerSlot.placedPart != null) count++;
        if (MagRequired     && magSlot != null     && magSlot.placedPart != null)     count++;
        if (BodyRequired    && bodySlot != null    && bodySlot.placedPart != null)    count++;
        return count;
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
