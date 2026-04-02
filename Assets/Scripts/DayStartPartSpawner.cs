using UnityEngine;

/// <summary>
/// Spawns a set of free gun parts at configured shelf positions at the start of each new day.
/// Attach to any GameObject in the scene. Assign spawn entries in the Inspector.
/// </summary>
public class DayStartPartSpawner : MonoBehaviour
{
    [System.Serializable]
    public class PartSpawnEntry
    {
        public PartType type;
        public GameObject prefab;
        public Transform spawnPoint;
    }

    [Header("Free Parts Per Day")]
    [Tooltip("Parts that spawn for free at the start of every day.")]
    public PartSpawnEntry[] freePartSpawns;

    int lastKnownDay = -1;

    void Awake()
    {
        RuntimeGameBootstrap.EnsureCoreSystems();
    }

    void Start()
    {
        // Record the day we start on so we don't double-spawn day 1
        lastKnownDay = DayManager.HasLiveInstance ? DayManager.Instance.CurrentDay : -1;
        SpawnFreePartsForDay();
    }

    void Update()
    {
        if (!DayManager.HasLiveInstance) return;

        int currentDay = DayManager.Instance.CurrentDay;
        if (currentDay != lastKnownDay && DayManager.Instance.IsDayActive)
        {
            lastKnownDay = currentDay;
            SpawnFreePartsForDay();
        }
    }

    void SpawnFreePartsForDay()
    {
        if (freePartSpawns == null || freePartSpawns.Length == 0) return;

        int spawned = 0;
        for (int i = 0; i < freePartSpawns.Length; i++)
        {
            PartSpawnEntry entry = freePartSpawns[i];
            if (entry == null || entry.prefab == null || entry.spawnPoint == null) continue;

            Vector3 pos = entry.spawnPoint.position + Vector3.up * 0.1f * i; // slight offset to avoid overlap
            Instantiate(entry.prefab, pos, entry.spawnPoint.rotation);
            spawned++;
        }

        if (spawned > 0)
        {
            GameFeedback.Show($"{spawned} free part(s) have arrived at the storage shelves.", 3f);
        }
    }
}
