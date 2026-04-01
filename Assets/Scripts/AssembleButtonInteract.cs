using UnityEngine;

public class AssembleButtonInteract : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    public void Interact()
    {
        // Only work if ready
        if (CraftingManager.Instance != null && CraftingManager.Instance.canAssemble)
        {
            CraftingManager.Instance.AssembleGun();
        }
        else
        {
            string message = CraftingManager.Instance != null
                ? CraftingManager.Instance.GetMissingSlotsSummary()
                : "The assembly bench is not ready yet.";
            GameFeedback.Show(message, 2f);
        }
    }

    public string GetInteractionPrompt()
    {
        if (CraftingManager.Instance == null)
        {
            return "Assembly system offline";
        }

        return CraftingManager.Instance.canAssemble
            ? "Press E to assemble the gun"
            : CraftingManager.Instance.GetMissingSlotsSummary();
    }
}
