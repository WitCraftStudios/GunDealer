using UnityEngine;

public class Part : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    public PartType type; // Set in Inspector for each prefab (e.g., GripA)

    public void Interact()
    {
        PartSlot slot = GetComponentInParent<PartSlot>();
        if (slot != null && slot.placedPart == this)
        {
            slot.Interact();
            return;
        }

        if (PlayerInventory.Instance == null)
        {
            GameFeedback.Error("The inventory system is missing.");
            return;
        }

        // Picked up by player
        if (PlayerInventory.Instance.PickUpItem(gameObject))
        {
            GameFeedback.Show($"Picked up {OrderManager.GetPartTypeLabel(type)}.");
        }
    }

    public string GetInteractionPrompt()
    {
        PartSlot slot = GetComponentInParent<PartSlot>();
        if (slot != null && slot.placedPart == this)
        {
            return $"Press E to take {OrderManager.GetPartTypeLabel(type)}";
        }

        return $"Press E to pick up {OrderManager.GetPartTypeLabel(type)}";
    }
}
