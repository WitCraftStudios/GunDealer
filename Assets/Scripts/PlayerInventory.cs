using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance;

    [Header("Refs")]
    public Transform handPosition; // Empty child on Player, Pos (0.5, 1.2, 0.8) local (right hand sim)

    public GameObject heldItem; // Changed to general GameObject

    const float HeldItemPadding = 0.04f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate PlayerInventory found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void LateUpdate()
    {
        ResolveHeldItemClipping();
    }

    public bool PickUpItem(GameObject item)
    {
        if (item == null)
        {
            GameFeedback.Warn("There is nothing to pick up.");
            return false;
        }

        if (handPosition == null)
        {
            GameFeedback.Error("The player hand position is missing.");
            return false;
        }

        if (heldItem != null)
        {
            GameFeedback.Show("Your hands are full.", 1.7f);
            return false; // Can't hold more than one
        }

        // Pick up
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            PrepareRigidbody(rb, true);
        }

        item.transform.SetParent(handPosition);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        heldItem = item;
        return true;
    }

    public void DropItem()
    {
        GameObject droppedItem = ReleaseHeldItem(true);
        if (droppedItem == null)
        {
            GameFeedback.Show("You are not holding anything.", 1.5f);
            return;
        }

        // Push slightly forward to avoid clipping player collider
        droppedItem.transform.position += transform.forward * 0.5f; // Push forward relative to player
        GameFeedback.Show($"Dropped {droppedItem.name.Trim()}.");
    }

    // Called when placing into a slot (we don't want physics enabled)
    public void ForceClearHeldItem()
    {
        heldItem = null;
    }

    public GameObject ReleaseHeldItem(bool enablePhysics)
    {
        if (heldItem == null) return null;

        GameObject item = heldItem;
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) PrepareRigidbody(rb, !enablePhysics);

        item.transform.SetParent(null);
        heldItem = null;
        return item;
    }

    public bool IsHoldingAssembledGun()
    {
        if (heldItem == null) return false;
        Part part = heldItem.GetComponent<Part>();
        return part != null && part.type == PartType.Assembled;
    }

    void PrepareRigidbody(Rigidbody rb, bool makeKinematic)
    {
        bool wasKinematic = rb.isKinematic;
        if (wasKinematic)
        {
            rb.isKinematic = false;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = makeKinematic;
    }

    void ResolveHeldItemClipping()
    {
        if (heldItem == null || handPosition == null) return;

        Camera mainCamera = Camera.main;
        Vector3 origin = mainCamera != null ? mainCamera.transform.position : transform.position + Vector3.up * 1.5f;
        Vector3 targetPosition = handPosition.position;
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
        {
            heldItem.transform.position = targetPosition;
            heldItem.transform.rotation = handPosition.rotation;
            return;
        }

        float castRadius = GetHeldItemCastRadius();
        Vector3 normalizedDirection = direction / distance;
        RaycastHit[] hits = Physics.SphereCastAll(origin, castRadius, normalizedDirection, distance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        Vector3 safePosition = targetPosition;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;
            if (hitCollider.transform.IsChildOf(heldItem.transform)) continue;

            float safeDistance = Mathf.Max(0.08f, hits[i].distance - castRadius - HeldItemPadding);
            safePosition = origin + normalizedDirection * safeDistance;
            break;
        }

        heldItem.transform.position = safePosition;
        heldItem.transform.rotation = handPosition.rotation;
    }

    float GetHeldItemCastRadius()
    {
        if (heldItem == null) return 0.12f;

        Collider itemCollider = heldItem.GetComponentInChildren<Collider>();
        if (itemCollider == null) return 0.12f;

        Vector3 extents = itemCollider.bounds.extents;
        float radius = Mathf.Max(extents.x, extents.y, extents.z) * 0.55f;
        return Mathf.Clamp(radius, 0.08f, 0.28f);
    }
}
