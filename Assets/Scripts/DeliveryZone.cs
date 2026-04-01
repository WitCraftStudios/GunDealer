using System.Collections.Generic;
using UnityEngine;

public class DeliveryZone : MonoBehaviour
{
    public static DeliveryZone Instance;
    public Transform spawnPoint; // Set in Inspector
    public float slotSpacingX = 1.05f;
    public float slotSpacingZ = 0.9f;
    public float stackHeight = 0.22f;
    public float slotReuseRadius = 0.65f;

    Transform runtimeSlotRoot;
    readonly Dictionary<string, Transform> runtimeSlots = new Dictionary<string, Transform>();
    readonly Dictionary<string, List<GameObject>> slotContents = new Dictionary<string, List<GameObject>>();

    static readonly string[] DefaultSlotOrder =
    {
        "guncase",
        "verticalgrip",
        "horizontalgrip",
        "trigger1",
        "trigger2",
        "longmag",
        "shortmag",
        "bodyar",
        "bodyak"
    };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate DeliveryZone found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        Instance = this;
        if (spawnPoint == null) spawnPoint = transform;
        EnsureRuntimeSlots();
    }

    public void SpawnItem(ShopItem item)
    {
        if (item == null || item.prefab == null)
        {
            Debug.LogError("DeliveryZone is missing a shop item or prefab.");
            return;
        }

        SpawnItemInternal(item.prefab, ResolveSlotId(item.itemName, item.prefab.name));
    }

    public void SpawnItem(GameObject prefab)
    {
        if (prefab == null || spawnPoint == null)
        {
            Debug.LogError("DeliveryZone is missing a prefab or spawn point.");
            return;
        }

        SpawnItemInternal(prefab, ResolveSlotId(prefab.name, prefab.name));
    }

    void SpawnItemInternal(GameObject prefab, string slotId)
    {
        if (prefab == null || spawnPoint == null)
        {
            Debug.LogError("DeliveryZone is missing a prefab or spawn point.");
            return;
        }

        EnsureRuntimeSlots();
        Transform slot = GetSlot(slotId);
        Vector3 spawnPosition = GetSpawnPosition(slotId, slot);
        GameObject spawned = Instantiate(prefab, spawnPosition, slot.rotation);
        PrepareSpawnedItem(spawned);
        RegisterSpawn(slotId, spawned);
        Debug.Log("Item arrived at Delivery Zone!");
    }

    void PrepareSpawnedItem(GameObject spawned)
    {
        if (spawned == null) return;

        Rigidbody rb = spawned.GetComponent<Rigidbody>();
        if (rb != null)
        {
            bool restoreKinematic = rb.isKinematic;
            if (restoreKinematic)
            {
                rb.isKinematic = false;
            }

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
            rb.isKinematic = restoreKinematic;
        }
    }

    void EnsureRuntimeSlots()
    {
        Transform origin = spawnPoint != null ? spawnPoint : transform;
        if (origin == null) return;

        if (runtimeSlotRoot == null)
        {
            Transform existingRoot = origin.Find("RuntimeDeliverySlots");
            if (existingRoot != null)
            {
                runtimeSlotRoot = existingRoot;
            }
            else
            {
                GameObject slotRootObject = new GameObject("RuntimeDeliverySlots");
                runtimeSlotRoot = slotRootObject.transform;
                runtimeSlotRoot.SetParent(origin, false);
                runtimeSlotRoot.localPosition = Vector3.zero;
                runtimeSlotRoot.localRotation = Quaternion.identity;
            }
        }

        for (int i = 0; i < DefaultSlotOrder.Length; i++)
        {
            string slotId = DefaultSlotOrder[i];
            if (runtimeSlots.ContainsKey(slotId) && runtimeSlots[slotId] != null)
            {
                continue;
            }

            Transform existing = runtimeSlotRoot.Find($"Slot_{slotId}");
            if (existing == null)
            {
                GameObject slotObject = new GameObject($"Slot_{slotId}");
                existing = slotObject.transform;
                existing.SetParent(runtimeSlotRoot, false);
                int column = i % 3;
                int row = i / 3;
                existing.localPosition = new Vector3((column - 1) * slotSpacingX, 0f, -row * slotSpacingZ);
                existing.localRotation = Quaternion.identity;
            }

            runtimeSlots[slotId] = existing;
            if (!slotContents.ContainsKey(slotId))
            {
                slotContents[slotId] = new List<GameObject>();
            }
        }
    }

    Transform GetSlot(string slotId)
    {
        EnsureRuntimeSlots();
        if (runtimeSlots.TryGetValue(slotId, out Transform slot) && slot != null)
        {
            return slot;
        }

        if (runtimeSlotRoot == null)
        {
            return spawnPoint != null ? spawnPoint : transform;
        }

        GameObject slotObject = new GameObject($"Slot_{slotId}");
        slot = slotObject.transform;
        slot.SetParent(runtimeSlotRoot, false);
        slot.localPosition = Vector3.zero;
        slot.localRotation = Quaternion.identity;
        runtimeSlots[slotId] = slot;
        if (!slotContents.ContainsKey(slotId))
        {
            slotContents[slotId] = new List<GameObject>();
        }

        return slot;
    }

    Vector3 GetSpawnPosition(string slotId, Transform slot)
    {
        if (slot == null)
        {
            return spawnPoint != null ? spawnPoint.position : transform.position;
        }

        PruneSlot(slotId, slot);
        int stackIndex = slotContents.TryGetValue(slotId, out List<GameObject> items) ? items.Count : 0;
        return slot.position + slot.up * (stackHeight * stackIndex);
    }

    void RegisterSpawn(string slotId, GameObject spawned)
    {
        if (spawned == null) return;

        if (!slotContents.ContainsKey(slotId))
        {
            slotContents[slotId] = new List<GameObject>();
        }

        slotContents[slotId].Add(spawned);
    }

    void PruneSlot(string slotId, Transform slot)
    {
        if (slot == null)
        {
            return;
        }

        if (!slotContents.TryGetValue(slotId, out List<GameObject> items))
        {
            slotContents[slotId] = new List<GameObject>();
            return;
        }

        Vector2 slotPlanar = new Vector2(slot.position.x, slot.position.z);
        for (int i = items.Count - 1; i >= 0; i--)
        {
            GameObject item = items[i];
            if (item == null)
            {
                items.RemoveAt(i);
                continue;
            }

            Vector2 itemPlanar = new Vector2(item.transform.position.x, item.transform.position.z);
            if (Vector2.Distance(itemPlanar, slotPlanar) > slotReuseRadius)
            {
                items.RemoveAt(i);
            }
        }
    }

    string ResolveSlotId(string itemName, string prefabName)
    {
        string normalizedItemName = NormalizeSlotId(itemName);
        if (!string.IsNullOrWhiteSpace(normalizedItemName))
        {
            return normalizedItemName;
        }

        string normalizedPrefabName = NormalizeSlotId(prefabName);
        return string.IsNullOrWhiteSpace(normalizedPrefabName) ? "misc" : normalizedPrefabName;
    }

    string NormalizeSlotId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char current = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(current))
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
