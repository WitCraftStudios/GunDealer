using UnityEngine;

public class GunCase : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    public Transform gunSnapPoint; // Child empty for snap
    public GameObject lid; // Drag Lid child here in Inspector
    private GameObject placedGun;
    private bool isInTransit;

    public bool HasLoadedGun => placedGun != null;
    public bool IsInTransit => isInTransit;

    public void Interact()
    {
        if (isInTransit)
        {
            GameFeedback.Show("That case is already moving down the conveyor.", 1.8f);
            return;
        }

        if (PlayerInventory.Instance == null)
        {
            GameFeedback.Error("The inventory system is missing.");
            return;
        }

        if (PlayerInventory.Instance.IsHoldingAssembledGun())
        {
            if (placedGun != null)
            {
                GameFeedback.Show("This case already has a gun inside.", 1.8f);
                return;
            }

            if (gunSnapPoint == null)
            {
                GameFeedback.Error("This case is missing its snap point.");
                return;
            }

            GameObject held = PlayerInventory.Instance.heldItem;
            Rigidbody rb = held.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            held.transform.SetParent(gunSnapPoint);
            held.transform.localPosition = Vector3.zero;
            held.transform.localRotation = Quaternion.identity;
            
            // Fix Physics Jitter: Disable collider so it doesn't fight with the case
            Collider col = held.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            placedGun = held;
            PlayerInventory.Instance.ForceClearHeldItem();
            CloseCase();
            GameFeedback.Show("Loaded the assembled gun into the case.");
        }
        else
        {
            // Pick up the case
            if (PlayerInventory.Instance.PickUpItem(gameObject))
            {
                GameFeedback.Show(placedGun != null ? "Picked up the loaded gun case." : "Picked up the empty gun case.");
            }
        }
    }

    public string GetInteractionPrompt()
    {
        if (isInTransit)
        {
            return "Case in transit";
        }

        if (PlayerInventory.Instance != null && PlayerInventory.Instance.IsHoldingAssembledGun())
        {
            return placedGun == null ? "Press E to load the gun into the case" : "Case already loaded";
        }

        return placedGun == null ? "Press E to pick up the empty case" : "Press E to pick up the loaded case";
    }

    public void BeginTransit()
    {
        isInTransit = true;
        SetCaseCollidersEnabled(false);
    }

    private void CloseCase()
    {
        if (lid != null) lid.SetActive(false);
    }

    void SetCaseCollidersEnabled(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (placedGun != null && col.transform.IsChildOf(placedGun.transform)) continue;
            col.enabled = enabled;
        }
    }
}
