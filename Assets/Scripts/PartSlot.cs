using UnityEngine;

public class PartSlot : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    public PartType requiredType; // Set in Inspector to a representative type, e.g., GripA for grip slot
    public Part placedPart; // Public as fixed earlier

    public void Interact()
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            GameFeedback.Error("The inventory system is missing.");
            return;
        }

        // 1. If slot is full, give the part back to player (if empty handed)
        if (placedPart != null)
        {
            if (inventory.heldItem == null)
            {
                Part partToReturn = placedPart;
                if (inventory.PickUpItem(partToReturn.gameObject))
                {
                    placedPart = null;
                    if (CraftingManager.Instance != null) CraftingManager.Instance.CheckAssembly();
                    GameFeedback.Show($"Took {OrderManager.GetPartTypeLabel(partToReturn.type)} from the bench.");
                }
            }
            else
            {
                GameFeedback.Show("Free your hands before taking the part back.", 1.8f);
            }
            return;
        }

        // 2. If slot empty, try to place part
        GameObject heldObj = inventory.heldItem;
        if (heldObj == null)
        {
            GameFeedback.Show($"Hold a {GetRequiredGroupLabel().ToLower()} to use this slot.", 1.8f);
            return;
        }

        Part held = heldObj.GetComponent<Part>();
        if (held == null)
        {
            GameFeedback.Show("That item cannot be placed on the assembly bench.", 1.8f);
            return;
        }

        if (MatchesRequiredGroup(held.type))
        {
            // Place part
            Rigidbody heldRb = heldObj.GetComponent<Rigidbody>();
            if (heldRb != null) heldRb.isKinematic = true;

            heldObj.transform.SetParent(transform);
            heldObj.transform.localPosition = Vector3.zero;
            heldObj.transform.localRotation = Quaternion.identity;
            placedPart = held;

            inventory.ForceClearHeldItem();

            if (CraftingManager.Instance != null) CraftingManager.Instance.CheckAssembly();
            GameFeedback.Show($"Placed {OrderManager.GetPartTypeLabel(held.type)} on the bench.");

            // Tutorial hook
            TutorialManager.FirePartPlaced();
        }
        else
        {
            GameFeedback.Show($"This slot needs {GetRequiredGroupLabel().ToLower()}.", 2f);
        }
    }

    public string GetInteractionPrompt()
    {
        if (placedPart != null)
        {
            return $"Press E to take {OrderManager.GetPartTypeLabel(placedPart.type)}";
        }

        if (PlayerInventory.Instance != null && PlayerInventory.Instance.heldItem != null)
        {
            Part held = PlayerInventory.Instance.heldItem.GetComponent<Part>();
            if (held == null)
            {
                return "Hold a compatible weapon part";
            }

            if (MatchesRequiredGroup(held.type))
            {
                return $"Press E to place {OrderManager.GetPartTypeLabel(held.type)}";
            }

            return $"Needs {GetRequiredGroupLabel()}";
        }

        return $"{GetRequiredGroupLabel()} slot";
    }

    string GetRequiredGroupLabel()
    {
        switch (requiredType)
        {
            case PartType.GripA:
            case PartType.GripB:
                return "Grip";
            case PartType.Trigger1:
            case PartType.Trigger2:
                return "Trigger";
            case PartType.MagShort:
            case PartType.MagLong:
                return "Magazine";
            case PartType.BodyAK:
            case PartType.BodyAR:
                return "Body";
            default:
                return "Part";
        }
    }

    bool MatchesRequiredGroup(PartType heldType)
    {
        switch (requiredType)
        {
            case PartType.GripA:
            case PartType.GripB:
                return heldType == PartType.GripA || heldType == PartType.GripB;
            case PartType.Trigger1:
            case PartType.Trigger2:
                return heldType == PartType.Trigger1 || heldType == PartType.Trigger2;
            case PartType.MagShort:
            case PartType.MagLong:
                return heldType == PartType.MagShort || heldType == PartType.MagLong;
            case PartType.BodyAK:
            case PartType.BodyAR:
                return heldType == PartType.BodyAK || heldType == PartType.BodyAR;
            default:
                return false;
        }
    }
}
