using UnityEngine;

public interface IInteractable
{
    void Interact();
}

public interface IInteractionPromptProvider
{
    string GetInteractionPrompt();
}
