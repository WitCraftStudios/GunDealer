using UnityEngine;

public class ComputerInteract : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    public void Interact()
    {
        // View the order (Pull)
        if (OrderManager.Instance != null) OrderManager.Instance.ViewOrder();
        else if (PCManager.Instance != null) PCManager.Instance.EnterPC();
    }

    public string GetInteractionPrompt()
    {
        return "Press E to use the computer";
    }
}
