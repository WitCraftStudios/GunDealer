using System.Collections.Generic;
using UnityEngine;

public class ForestEncounterManager : MonoBehaviour
{
    private static ForestEncounterManager instance;

    [Header("Forest Layout")]
    public Vector2 forestHalfExtents = new Vector2(44f, 44f);
    public Vector2 clearingHalfExtents = new Vector2(17f, 17f);
    public float wallHeight = 8f;
    public float wallThickness = 1.5f;
    public int treeGridSpacing = 8;

    [Header("Combat")]
    public int lowRiskNpcCount = 1;
    public int mediumRiskNpcCount = 2;
    public int highRiskNpcCount = 3;

    Transform environmentRoot;
    readonly List<Transform> npcSpawnPoints = new List<Transform>();
    readonly List<ForestNpc> activeNpcs = new List<ForestNpc>();
    Material forestGroundMaterial;
    Material wallMaterial;
    Material trunkMaterial;
    Material foliageMaterial;
    Material rangeMaterial;
    Material spawnPointMaterial;

    public static bool HasLiveInstance => instance != null || FindFirstObjectByType<ForestEncounterManager>() != null;

    public static ForestEncounterManager Instance
    {
        get
        {
            if (instance == null) instance = FindFirstObjectByType<ForestEncounterManager>();
            if (instance == null)
            {
                GameObject go = new GameObject("ForestEncounterManager");
                instance = go.AddComponent<ForestEncounterManager>();
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        EnsureEnvironment();
    }

    void Start()
    {
        EnsureEnvironment();
    }

    public void EnsureEnvironment()
    {
        Vector3 center = DetermineForestCenter();
        EnsureMaterials();

        if (environmentRoot == null)
        {
            Transform existing = transform.Find("ForestCombatZone");
            if (existing != null) environmentRoot = existing;
        }

        if (environmentRoot == null)
        {
            GameObject root = new GameObject("ForestCombatZone");
            root.transform.SetParent(transform, false);
            environmentRoot = root.transform;
        }

        BuildGround(center);
        BuildPerimeterWalls(center);
        BuildForestTrees(center);
        BuildTestingRange(center);
        BuildNpcSpawnPoints(center);
    }

    public void NotifyOrderAccepted(GunOrder order)
    {
        EnsureEnvironment();
        ClearActiveNpcs();

        int spawnCount = GetNpcCount(order != null ? order.riskLevel : OrderRisk.Low);
        Transform player = FindFirstObjectByType<PlayerController>()?.transform;
        if (player == null || npcSpawnPoints.Count == 0) return;

        List<int> usedIndices = new List<int>();
        for (int i = 0; i < spawnCount; i++)
        {
            int index = SelectSpawnIndex(usedIndices);
            if (index < 0) break;
            usedIndices.Add(index);

            Transform spawnPoint = npcSpawnPoints[index];
            GameObject npcObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcObject.name = order != null ? $"{order.gunName}_Scout_{i + 1}" : $"ForestScout_{i + 1}";
            npcObject.transform.SetParent(environmentRoot, true);
            npcObject.transform.position = spawnPoint.position + Vector3.up;
            npcObject.transform.localScale = new Vector3(1.15f, 1.1f, 1.15f);
            npcObject.GetComponent<Renderer>().material = new Material(rangeMaterial) { color = new Color(0.78f, 0.73f, 0.62f, 1f) };

            ForestNpc npc = npcObject.AddComponent<ForestNpc>();
            npc.Initialize(player);
            activeNpcs.Add(npc);
        }

        if (spawnCount > 0)
        {
            GameFeedback.Show("Movement in the tree line. Forest scouts are active.", 2.4f);
        }
    }

    public void ClearActiveNpcs()
    {
        for (int i = activeNpcs.Count - 1; i >= 0; i--)
        {
            ForestNpc npc = activeNpcs[i];
            if (npc != null) Destroy(npc.gameObject);
        }

        activeNpcs.Clear();
    }

    void BuildGround(Vector3 center)
    {
        if (environmentRoot.Find("ForestGround") != null) return;

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "ForestGround";
        ground.transform.SetParent(environmentRoot, false);
        ground.transform.position = new Vector3(center.x, -0.18f, center.z);
        ground.transform.localScale = new Vector3(forestHalfExtents.x / 5f, 1f, forestHalfExtents.y / 5f);
        ground.GetComponent<Renderer>().material = forestGroundMaterial;
    }

    void BuildPerimeterWalls(Vector3 center)
    {
        if (environmentRoot.Find("PerimeterWalls") != null) return;

        GameObject wallsRoot = new GameObject("PerimeterWalls");
        wallsRoot.transform.SetParent(environmentRoot, false);

        CreateWall(
            "NorthWall",
            wallsRoot.transform,
            new Vector3(center.x, wallHeight * 0.5f, center.z + forestHalfExtents.y),
            new Vector3(forestHalfExtents.x * 2f + wallThickness, wallHeight, wallThickness));
        CreateWall(
            "SouthWall",
            wallsRoot.transform,
            new Vector3(center.x, wallHeight * 0.5f, center.z - forestHalfExtents.y),
            new Vector3(forestHalfExtents.x * 2f + wallThickness, wallHeight, wallThickness));
        CreateWall(
            "EastWall",
            wallsRoot.transform,
            new Vector3(center.x + forestHalfExtents.x, wallHeight * 0.5f, center.z),
            new Vector3(wallThickness, wallHeight, forestHalfExtents.y * 2f + wallThickness));
        CreateWall(
            "WestWall",
            wallsRoot.transform,
            new Vector3(center.x - forestHalfExtents.x, wallHeight * 0.5f, center.z),
            new Vector3(wallThickness, wallHeight, forestHalfExtents.y * 2f + wallThickness));
    }

    void BuildForestTrees(Vector3 center)
    {
        if (environmentRoot.Find("Trees") != null) return;

        GameObject treesRoot = new GameObject("Trees");
        treesRoot.transform.SetParent(environmentRoot, false);

        int counter = 0;
        for (int x = -Mathf.RoundToInt(forestHalfExtents.x) + 6; x <= Mathf.RoundToInt(forestHalfExtents.x) - 6; x += treeGridSpacing)
        {
            for (int z = -Mathf.RoundToInt(forestHalfExtents.y) + 6; z <= Mathf.RoundToInt(forestHalfExtents.y) - 6; z += treeGridSpacing)
            {
                Vector3 offset = new Vector3(x, 0f, z);
                if (Mathf.Abs(offset.x) < clearingHalfExtents.x && Mathf.Abs(offset.z) < clearingHalfExtents.y) continue;
                if (offset.x > forestHalfExtents.x - 18f && Mathf.Abs(offset.z) < 11f) continue;

                Vector3 jitter = new Vector3(HashRange(counter, -1.8f, 1.8f), 0f, HashRange(counter + 73, -1.8f, 1.8f));
                CreateTree(treesRoot.transform, center + offset + jitter, counter++);
            }
        }
    }

    void BuildNpcSpawnPoints(Vector3 center)
    {
        npcSpawnPoints.Clear();

        Transform existingRoot = environmentRoot.Find("NpcSpawnPoints");
        if (existingRoot == null)
        {
            existingRoot = GameObject.Find("NpcSpawnPoints")?.transform;
        }

        if (existingRoot != null)
        {
            for (int i = 0; i < existingRoot.childCount; i++)
            {
                npcSpawnPoints.Add(existingRoot.GetChild(i));
            }
            return;
        }

        GameObject spawnRoot = new GameObject("NpcSpawnPoints");
        spawnRoot.transform.SetParent(environmentRoot, false);

        Vector3[] offsets =
        {
            new Vector3(-forestHalfExtents.x + 12f, 0f, -forestHalfExtents.y + 14f),
            new Vector3(-forestHalfExtents.x + 14f, 0f, forestHalfExtents.y - 14f),
            new Vector3(forestHalfExtents.x - 14f, 0f, -forestHalfExtents.y + 14f),
            new Vector3(forestHalfExtents.x - 12f, 0f, forestHalfExtents.y - 16f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject marker = new GameObject($"SpawnPoint_{i + 1}");
            marker.name = $"SpawnPoint_{i + 1}";
            marker.transform.SetParent(spawnRoot.transform, false);
            marker.transform.position = center + offsets[i];
            marker.transform.localRotation = Quaternion.identity;
            npcSpawnPoints.Add(marker.transform);
        }
    }

    void BuildTestingRange(Vector3 center)
    {
        if (environmentRoot.Find("TestingRange") != null) return;

        GameObject rangeRoot = new GameObject("TestingRange");
        rangeRoot.transform.SetParent(environmentRoot, false);

        Vector3 laneCenter = center + new Vector3(forestHalfExtents.x - 18f, 0f, 0f);

        CreateBlock(
            "LaneFloor",
            rangeRoot.transform,
            new Vector3(laneCenter.x, 0.12f, laneCenter.z),
            new Vector3(12f, 0.24f, 9f),
            rangeMaterial);
        CreateBlock(
            "Backstop",
            rangeRoot.transform,
            new Vector3(laneCenter.x + 5f, 3.2f, laneCenter.z),
            new Vector3(1.2f, 6.4f, 10f),
            wallMaterial);
        CreateBlock(
            "LeftLaneWall",
            rangeRoot.transform,
            new Vector3(laneCenter.x, 1.6f, laneCenter.z - 4.6f),
            new Vector3(12f, 3.2f, 0.5f),
            wallMaterial);
        CreateBlock(
            "RightLaneWall",
            rangeRoot.transform,
            new Vector3(laneCenter.x, 1.6f, laneCenter.z + 4.6f),
            new Vector3(12f, 3.2f, 0.5f),
            wallMaterial);

        float[] targetOffsets = { -2.8f, 0f, 2.8f };
        for (int i = 0; i < targetOffsets.Length; i++)
        {
            GameObject stand = CreateBlock(
                $"TargetStand_{i + 1}",
                rangeRoot.transform,
                new Vector3(laneCenter.x + 3.8f, 0.8f, laneCenter.z + targetOffsets[i]),
                new Vector3(0.35f, 1.6f, 0.35f),
                trunkMaterial);

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = $"Target_{i + 1}";
            target.transform.SetParent(stand.transform, false);
            target.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            target.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            target.transform.localScale = new Vector3(0.55f, 0.1f, 0.55f);
            target.AddComponent<RangeTarget>();
        }
    }

    void CreateTree(Transform parent, Vector3 position, int seed)
    {
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = $"Tree_{seed + 1}";
        trunk.transform.SetParent(parent, false);
        float height = HashRange(seed + 100, 3.6f, 5.6f);
        trunk.transform.position = position + Vector3.up * (height * 0.5f);
        trunk.transform.localScale = new Vector3(0.38f, height * 0.5f, 0.38f);
        trunk.GetComponent<Renderer>().material = trunkMaterial;

        GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        canopy.name = "Canopy";
        canopy.transform.SetParent(trunk.transform, false);
        canopy.transform.localPosition = new Vector3(0f, height * 0.55f, 0f);
        float canopyScale = HashRange(seed + 230, 2.4f, 3.8f);
        canopy.transform.localScale = Vector3.one * canopyScale;
        canopy.GetComponent<Renderer>().material = foliageMaterial;
    }

    GameObject CreateWall(string name, Transform parent, Vector3 position, Vector3 scale)
    {
        return CreateBlock(name, parent, position, scale, wallMaterial);
    }

    GameObject CreateBlock(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.position = position;
        block.transform.localScale = scale;
        Renderer renderer = block.GetComponent<Renderer>();
        if (renderer != null && material != null) renderer.material = material;
        return block;
    }

    Vector3 DetermineForestCenter()
    {
        Transform floor = GameObject.Find("Floor")?.transform;
        if (floor != null) return new Vector3(floor.position.x, 0f, floor.position.z);

        CraftingManager craftingManager = FindFirstObjectByType<CraftingManager>();
        if (craftingManager != null) return new Vector3(craftingManager.transform.position.x, 0f, craftingManager.transform.position.z);

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) return new Vector3(player.transform.position.x, 0f, player.transform.position.z);

        return Vector3.zero;
    }

    int GetNpcCount(OrderRisk risk)
    {
        switch (risk)
        {
            case OrderRisk.Medium:
                return mediumRiskNpcCount;
            case OrderRisk.High:
                return highRiskNpcCount;
            default:
                return lowRiskNpcCount;
        }
    }

    int SelectSpawnIndex(List<int> usedIndices)
    {
        if (npcSpawnPoints.Count == 0) return -1;

        for (int i = 0; i < npcSpawnPoints.Count; i++)
        {
            if (!usedIndices.Contains(i)) return i;
        }

        return -1;
    }

    void EnsureMaterials()
    {
        if (forestGroundMaterial == null) forestGroundMaterial = CreateMaterial(new Color(0.2f, 0.34f, 0.18f, 1f));
        if (wallMaterial == null) wallMaterial = CreateMaterial(new Color(0.17f, 0.18f, 0.2f, 1f));
        if (trunkMaterial == null) trunkMaterial = CreateMaterial(new Color(0.3f, 0.2f, 0.1f, 1f));
        if (foliageMaterial == null) foliageMaterial = CreateMaterial(new Color(0.18f, 0.44f, 0.22f, 1f));
        if (rangeMaterial == null) rangeMaterial = CreateMaterial(new Color(0.24f, 0.24f, 0.27f, 1f));
        if (spawnPointMaterial == null) spawnPointMaterial = CreateMaterial(new Color(0.76f, 0.64f, 0.2f, 1f));
    }

    Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
        material.color = color;
        return material;
    }

    float HashRange(int seed, float min, float max)
    {
        float normalized = Mathf.Abs(Mathf.Sin(seed * 12.9898f + 78.233f));
        return Mathf.Lerp(min, max, normalized);
    }
}
