using UnityEngine;

public class ConveyorInteract : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    public ConveyorManager conveyorManager;

    public void Interact()
    {
        if (PlayerInventory.Instance == null)
        {
            GameFeedback.Error("The inventory system is missing.");
            return;
        }

        if (conveyorManager == null)
        {
            GameFeedback.Error("The conveyor is not wired correctly.");
            return;
        }

        GameObject heldItem = PlayerInventory.Instance.heldItem;
        if (heldItem == null)
        {
            GameFeedback.Show("Hold a loaded gun case before using the conveyor.", 1.8f);
            return;
        }

        GunCase gunCase = heldItem.GetComponent<GunCase>();
        if (gunCase == null)
        {
            GameFeedback.Show("Only a loaded gun case can go on the conveyor.", 1.8f);
            return;
        }

        if (OrderManager.Instance == null || !OrderManager.Instance.HasAcceptedOrder)
        {
            GameFeedback.Show("Accept an order before shipping anything.", 1.8f);
            return;
        }

        if (!gunCase.HasLoadedGun)
        {
            GameFeedback.Show("Load the assembled gun into the case first.", 1.8f);
            return;
        }

        GameObject caseObj = PlayerInventory.Instance.ReleaseHeldItem(false);
        conveyorManager.DeliverCase(caseObj);
        GameFeedback.Show("Case dropped on the conveyor.");
    }

    public string GetInteractionPrompt()
    {
        if (PlayerInventory.Instance == null || PlayerInventory.Instance.heldItem == null)
        {
            return "Hold a loaded gun case to ship it";
        }

        GunCase gunCase = PlayerInventory.Instance.heldItem.GetComponent<GunCase>();
        if (gunCase == null)
        {
            return "Only gun cases can be shipped";
        }

        if (OrderManager.Instance == null || !OrderManager.Instance.HasAcceptedOrder)
        {
            return "Accept an order before shipping";
        }

        if (!gunCase.HasLoadedGun)
        {
            return "Load the assembled gun into the case first";
        }

        return "Press E to ship the case";
    }
}
