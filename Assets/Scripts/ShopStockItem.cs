using System.Collections.Generic;
using UnityEngine;

public class ShopStockItem : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    struct RigidbodyState
    {
        public Rigidbody body;
        public bool wasKinematic;
    }

    readonly List<MonoBehaviour> deferredInteractionBehaviours = new List<MonoBehaviour>();
    readonly List<RigidbodyState> rigidbodyStates = new List<RigidbodyState>();

    ShopManager shopManager;
    ShopItem item;
    bool isPurchased;

    public ShopItem Item => item;
    public bool IsPurchased => isPurchased;

    public void Initialize(ShopManager manager, ShopItem sourceItem)
    {
        shopManager = manager;
        item = sourceItem;
        isPurchased = false;
        CacheDeferredInteractions();
        LockItemInShop();
    }

    public void Interact()
    {
        if (isPurchased)
        {
            TryAutoCollect();
            return;
        }

        if (shopManager == null)
        {
            GameFeedback.Error("This shop item is not linked to the shop manager.");
            return;
        }

        shopManager.TryPurchaseWorldStock(this);
    }

    public string GetInteractionPrompt()
    {
        string itemName = item != null && !string.IsNullOrWhiteSpace(item.itemName) ? item.itemName : gameObject.name.Trim();

        if (isPurchased)
        {
            return $"Press E to pick up {itemName}";
        }

        if (RewardManager.Instance == null)
        {
            return $"Press E to buy {itemName}";
        }

        int cost = item != null ? item.cost : 0;
        return RewardManager.Instance.CurrentCash >= cost
            ? $"Press E to buy {itemName} for ${cost}"
            : $"Need ${cost} to buy {itemName}";
    }

    public void CompletePurchase()
    {
        if (isPurchased) return;

        isPurchased = true;
        RestoreItemPhysics();
        RestoreDeferredInteractions();
        transform.SetParent(null, true);
        Destroy(this);
    }

    public void TryAutoCollect()
    {
        if (!isPurchased || PlayerInventory.Instance == null || PlayerInventory.Instance.heldItem != null) return;

        Part part = GetComponent<Part>();
        if (part != null)
        {
            part.Interact();
            return;
        }

        GunCase gunCase = GetComponent<GunCase>();
        if (gunCase != null)
        {
            gunCase.Interact();
        }
    }

    void CacheDeferredInteractions()
    {
        deferredInteractionBehaviours.Clear();

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this) continue;
            if (!(behaviour is IInteractable) && !(behaviour is IInteractionPromptProvider)) continue;
            if (!behaviour.enabled) continue;

            behaviour.enabled = false;
            deferredInteractionBehaviours.Add(behaviour);
        }
    }

    void RestoreDeferredInteractions()
    {
        for (int i = 0; i < deferredInteractionBehaviours.Count; i++)
        {
            MonoBehaviour behaviour = deferredInteractionBehaviours[i];
            if (behaviour != null) behaviour.enabled = true;
        }

        deferredInteractionBehaviours.Clear();
    }

    void LockItemInShop()
    {
        rigidbodyStates.Clear();

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody body = rigidbodies[i];
            if (body == null) continue;

            rigidbodyStates.Add(new RigidbodyState
            {
                body = body,
                wasKinematic = body.isKinematic
            });

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    void RestoreItemPhysics()
    {
        for (int i = 0; i < rigidbodyStates.Count; i++)
        {
            RigidbodyState state = rigidbodyStates[i];
            if (state.body == null) continue;

            state.body.linearVelocity = Vector3.zero;
            state.body.angularVelocity = Vector3.zero;
            state.body.isKinematic = state.wasKinematic;
        }

        rigidbodyStates.Clear();
    }
}
