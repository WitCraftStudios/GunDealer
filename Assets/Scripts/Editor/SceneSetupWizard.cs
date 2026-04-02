#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Editor window that automates the manual scene setup described in the README.
/// Open via GunBlackMarket > Scene Setup Wizard.
/// </summary>
public class SceneSetupWizard : EditorWindow
{
    [MenuItem("GunBlackMarket/Scene Setup Wizard")]
    public static void ShowWindow()
    {
        GetWindow<SceneSetupWizard>("GBM Scene Setup Wizard");
    }

    Vector2 scroll;

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Gun Black Market — Scene Setup Wizard", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Run individual steps below, or click Setup Full Scene to run all of them at once.\n" +
            "Objects are only created if they don't already exist in the scene.",
            MessageType.Info);
        EditorGUILayout.Space(6);

        if (GUILayout.Button("1. Create Player", GUILayout.Height(30))) CreatePlayer();
        if (GUILayout.Button("2. Create Core Managers", GUILayout.Height(30))) CreateManagers();
        if (GUILayout.Button("3. Create PC Station", GUILayout.Height(30))) CreatePCStation();
        if (GUILayout.Button("4. Create Assembly Bench", GUILayout.Height(30))) CreateAssemblyBench();
        if (GUILayout.Button("5. Create Conveyor Belt", GUILayout.Height(30))) CreateConveyor();
        if (GUILayout.Button("6. Create Delivery Zone", GUILayout.Height(30))) CreateDeliveryZone();
        if (GUILayout.Button("7. Create Gun Case Dispenser", GUILayout.Height(30))) CreateGunCaseDispenser();
        if (GUILayout.Button("8. Create Day Start Part Spawner", GUILayout.Height(30))) CreateDayStartPartSpawner();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("─────────────────────────────", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(4);

        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.25f, 0.75f, 0.35f);
        if (GUILayout.Button("★  Setup Full Scene  ★", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Setup Full Scene",
                "This will create all GameObjects for the Gun Black Market scene. Continue?",
                "Yes, set it up", "Cancel"))
            {
                SetupFullScene();
            }
        }
        GUI.backgroundColor = prev;

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "After running setup:\n" +
            "• Assign ScriptableObject assets (GunOrders, ShopItems, DifficultyPresets) in the Managers inspector.\n" +
            "• Add ground/wall geometry and a matching Ground layer collider under the Floor object.\n" +
            "• Review the generated PC canvas and conveyor path positions in the scene.\n" +
            "• Press Play — RuntimeGameBootstrap will handle the rest.",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    // -------------------------------------------------------------------------
    // Individual steps
    // -------------------------------------------------------------------------

    static void SetupFullScene()
    {
        CreatePlayer();
        CreateManagers();
        CreatePCStation();
        CreateAssemblyBench();
        CreateConveyor();
        CreateDeliveryZone();
        CreateGunCaseDispenser();
        CreateDayStartPartSpawner();
        Debug.Log("[SceneSetupWizard] Full scene setup complete. Assign your ScriptableObject assets in the Inspector.");
    }

    static void CreatePlayer()
    {
        if (FindExisting("Player")) return;

        // Root
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        Undo.RegisterCreatedObjectUndo(player, "Create Player");

        // Capsule body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "PlayerBody";
        body.transform.SetParent(player.transform);
        body.transform.localPosition = new Vector3(0, 1f, 0);
        body.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
        // Remove collider from body — we'll add CharacterController to root
        DestroyImmediate(body.GetComponent<Collider>());

        // CharacterController
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0, 0.9f, 0);

        // Camera
        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        Camera cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 70f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.SetParent(player.transform);
        camGO.transform.localPosition = new Vector3(0, 1.6f, 0);

        // Hand position
        GameObject hand = new GameObject("HandPosition");
        hand.transform.SetParent(camGO.transform);
        hand.transform.localPosition = new Vector3(0.5f, -0.25f, 0.8f);

        // PlayerController script
        PlayerController pc = player.AddComponent<PlayerController>();
        PlayerInventory inv = player.AddComponent<PlayerInventory>();

        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (inputActions != null)
        {
            SerializedObject soPc = new SerializedObject(pc);
            soPc.FindProperty("inputActions")?.SetValue(inputActions);
            soPc.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedObject soInv = new SerializedObject(inv);
        soInv.FindProperty("handPosition")?.SetValue(hand.transform);
        soInv.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[SceneSetupWizard] Player created.");
    }

    static void CreateManagers()
    {
        EnsureManager<CampaignManager>("CampaignManager");
        EnsureManager<HeatManager>("HeatManager");
        EnsureManager<DayManager>("DayManager");
        EnsureManager<RewardManager>("RewardManager");
        EnsureManager<OrderManager>("OrderManager");
        EnsureManager<ShopManager>("ShopManager");
        EnsureManager<TimerManager>("TimerManager");
        EnsureManager<DifficultyManager>("DifficultyManager");
        Debug.Log("[SceneSetupWizard] Core managers created.");
    }

    static void CreatePCStation()
    {
        if (FindExisting("PCStation")) return;

        GameObject station = new GameObject("PCStation");
        Undo.RegisterCreatedObjectUndo(station, "Create PC Station");
        station.transform.position = new Vector3(3f, 0f, 0f);

        // Desk proxy
        GameObject desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        desk.name = "Desk";
        desk.transform.SetParent(station.transform);
        desk.transform.localPosition = new Vector3(0, 0.4f, 0);
        desk.transform.localScale    = new Vector3(1.8f, 0.8f, 0.9f);

        // PC interaction trigger volume
        GameObject pcBox = new GameObject("PCInteractPoint");
        pcBox.transform.SetParent(station.transform);
        pcBox.transform.localPosition = new Vector3(0, 1.0f, -0.3f);
        BoxCollider bc = pcBox.AddComponent<BoxCollider>();
        bc.isTrigger = false;
        bc.size = new Vector3(1.2f, 0.6f, 0.05f);
        pcBox.AddComponent<ComputerInteract>();

        // Camera view target
        GameObject viewTarget = new GameObject("ComputerViewTarget");
        viewTarget.transform.SetParent(station.transform);
        viewTarget.transform.localPosition = new Vector3(0, 1.2f, -0.6f);
        viewTarget.transform.localEulerAngles = new Vector3(10f, 0f, 0f);

        GameObject pcCanvas = CreateScreenSpaceCanvas("PCCanvas", station.transform);
        GameObject orderTab = CreateFillPanel(
            "OrderTab",
            pcCanvas.transform,
            new Color(0.05f, 0.07f, 0.09f, 0.92f),
            new Vector2(0.16f, 0.14f),
            new Vector2(0.84f, 0.86f));
        GameObject shopTab = CreateFillPanel(
            "ShopTab",
            pcCanvas.transform,
            new Color(0.06f, 0.08f, 0.1f, 0.92f),
            new Vector2(0.16f, 0.14f),
            new Vector2(0.84f, 0.86f));
        orderTab.SetActive(false);
        shopTab.SetActive(false);
        pcCanvas.SetActive(false);

        PlayerController playerController = Object.FindFirstObjectByType<PlayerController>();

        // PCManager — wire up
        PCManager pcm = EnsureManager<PCManager>("PCManager");
        SerializedObject soPcm = new SerializedObject(pcm);
        soPcm.FindProperty("pcCanvas")?.SetValue(pcCanvas);
        soPcm.FindProperty("orderTab")?.SetValue(orderTab);
        soPcm.FindProperty("shopTab")?.SetValue(shopTab);
        soPcm.FindProperty("computerViewTarget")?.SetValue(viewTarget.transform);
        soPcm.FindProperty("playerController")?.SetValue(playerController);
        soPcm.ApplyModifiedPropertiesWithoutUndo();

        OrderManager orderManager = EnsureManager<OrderManager>("OrderManager");
        SerializedObject soOrder = new SerializedObject(orderManager);
        soOrder.FindProperty("orderUIPanel")?.SetValue(orderTab);
        soOrder.FindProperty("computerViewTarget")?.SetValue(viewTarget.transform);
        soOrder.FindProperty("playerController")?.SetValue(playerController);
        soOrder.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[SceneSetupWizard] PC Station created and wired to PCManager and OrderManager.");
    }

    static void CreateAssemblyBench()
    {
        if (FindExisting("AssemblyBench")) return;

        GameObject bench = new GameObject("AssemblyBench");
        Undo.RegisterCreatedObjectUndo(bench, "Create Assembly Bench");
        bench.transform.position = new Vector3(-2f, 0f, 1f);

        // Table surface
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = "BenchSurface";
        surface.transform.SetParent(bench.transform);
        surface.transform.localPosition = new Vector3(0, 0.4f, 0);
        surface.transform.localScale    = new Vector3(2f, 0.8f, 1.2f);

        // Four part slots
        string[] slotNames = { "GripSlot", "TriggerSlot", "MagSlot", "BodySlot" };
        Vector3[] slotOffsets =
        {
            new Vector3(-0.6f, 0.85f,  0.2f),
            new Vector3(-0.2f, 0.85f,  0.2f),
            new Vector3( 0.2f, 0.85f,  0.2f),
            new Vector3( 0.6f, 0.85f,  0.2f),
        };
        PartType[] defaultTypes = { PartType.GripA, PartType.Trigger1, PartType.MagShort, PartType.BodyAK };
        PartSlot[] slots = new PartSlot[4];

        for (int i = 0; i < 4; i++)
        {
            GameObject slotGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slotGO.name = slotNames[i];
            slotGO.transform.SetParent(bench.transform);
            slotGO.transform.localPosition = slotOffsets[i];
            slotGO.transform.localScale    = new Vector3(0.28f, 0.08f, 0.28f);
            slotGO.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial(new Color(0.2f, 0.5f, 0.9f, 1f));
            PartSlot ps = slotGO.AddComponent<PartSlot>();
            ps.requiredType = defaultTypes[i];
            slots[i] = ps;
        }

        // Assemble trigger button
        GameObject assembleBtn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        assembleBtn.name = "AssembleButton";
        assembleBtn.transform.SetParent(bench.transform);
        assembleBtn.transform.localPosition = new Vector3(0f, 0.85f, -0.4f);
        assembleBtn.transform.localScale    = new Vector3(0.5f, 0.08f, 0.24f);
        assembleBtn.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial(new Color(0.85f, 0.15f, 0.15f, 1f));
        assembleBtn.AddComponent<AssembleButtonInteract>();

        // CraftingManager — wire up all slots
        CraftingManager cm = bench.AddComponent<CraftingManager>();
        SerializedObject soCm = new SerializedObject(cm);
        soCm.FindProperty("gripSlot")?.SetValue(slots[0]);
        soCm.FindProperty("triggerSlot")?.SetValue(slots[1]);
        soCm.FindProperty("magSlot")?.SetValue(slots[2]);
        soCm.FindProperty("bodySlot")?.SetValue(slots[3]);
        soCm.FindProperty("assembledGunSpawnPoint")?.SetValue(bench.transform);
        soCm.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[SceneSetupWizard] Assembly Bench created. Assign the FinalGunPrefab in CraftingManager.");
    }

    static void CreateConveyor()
    {
        if (FindExisting("ConveyorBelt")) return;

        GameObject conveyor = new GameObject("ConveyorBelt");
        Undo.RegisterCreatedObjectUndo(conveyor, "Create Conveyor");
        conveyor.transform.position = new Vector3(0f, 0f, 4f);

        GameObject belt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        belt.name = "Belt";
        belt.transform.SetParent(conveyor.transform);
        belt.transform.localPosition = new Vector3(0, 0.5f, 0);
        belt.transform.localScale    = new Vector3(1.2f, 0.12f, 3f);
        belt.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial(new Color(0.1f, 0.1f, 0.1f));

        // Interact point
        GameObject interactPoint = new GameObject("ConveyorInteractPoint");
        interactPoint.transform.SetParent(conveyor.transform);
        interactPoint.transform.localPosition = new Vector3(0, 0.9f, -1.2f);
        BoxCollider trigBC = interactPoint.AddComponent<BoxCollider>();
        trigBC.size = new Vector3(1f, 0.5f, 0.05f);

        ConveyorManager cm = conveyor.AddComponent<ConveyorManager>();
        GameObject dropPoint = new GameObject("DropPoint");
        dropPoint.transform.SetParent(conveyor.transform);
        dropPoint.transform.localPosition = new Vector3(0f, 0.7f, -1.15f);
        dropPoint.transform.localRotation = Quaternion.identity;

        GameObject endPoint = new GameObject("EndPoint");
        endPoint.transform.SetParent(conveyor.transform);
        endPoint.transform.localPosition = new Vector3(0f, 0.7f, 1.15f);
        endPoint.transform.localRotation = Quaternion.identity;

        SerializedObject soCm = new SerializedObject(cm);
        soCm.FindProperty("dropPoint")?.SetValue(dropPoint.transform);
        soCm.FindProperty("endPoint")?.SetValue(endPoint.transform);
        soCm.ApplyModifiedPropertiesWithoutUndo();

        ConveyorInteract ci = interactPoint.AddComponent<ConveyorInteract>();
        SerializedObject soCi = new SerializedObject(ci);
        soCi.FindProperty("conveyorManager")?.SetValue(cm);
        soCi.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[SceneSetupWizard] Conveyor Belt created and wired with a delivery path.");
    }

    static void CreateDeliveryZone()
    {
        if (FindExisting("DeliveryZone")) return;

        GameObject deliveryZone = new GameObject("DeliveryZone");
        Undo.RegisterCreatedObjectUndo(deliveryZone, "Create Delivery Zone");
        deliveryZone.transform.position = new Vector3(-4f, 0f, -2f);

        GameObject spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.SetParent(deliveryZone.transform, false);
        spawnPoint.transform.localPosition = Vector3.zero;
        spawnPoint.transform.localRotation = Quaternion.identity;

        DeliveryZone zone = deliveryZone.AddComponent<DeliveryZone>();
        SerializedObject soZone = new SerializedObject(zone);
        soZone.FindProperty("spawnPoint")?.SetValue(spawnPoint.transform);
        soZone.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[SceneSetupWizard] Delivery Zone created for shop purchases.");
    }

    static void CreateGunCaseDispenser()
    {
        if (FindExisting("GunCaseDispenser")) return;

        GameObject dispenser = new GameObject("GunCaseDispenser");
        Undo.RegisterCreatedObjectUndo(dispenser, "Create GunCase Dispenser");
        dispenser.transform.position = new Vector3(-3.5f, 0f, 1.5f);

        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "CaseShelf";
        shelf.transform.SetParent(dispenser.transform);
        shelf.transform.localPosition = new Vector3(0, 0.5f, 0);
        shelf.transform.localScale = new Vector3(0.8f, 1f, 0.6f);
        shelf.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial(new Color(0.45f, 0.3f, 0.15f));

        Debug.Log("[SceneSetupWizard] Gun Case Dispenser placeholder created. Assign GunCase prefab to the shelf.");
    }

    static void CreateDayStartPartSpawner()
    {
        if (FindExisting("DayStartPartSpawner")) return;

        GameObject spawnerGO = new GameObject("DayStartPartSpawner");
        Undo.RegisterCreatedObjectUndo(spawnerGO, "Create DayStartPartSpawner");
        spawnerGO.transform.position = new Vector3(-4f, 0f, 0f);
        spawnerGO.AddComponent<DayStartPartSpawner>();
        Debug.Log("[SceneSetupWizard] DayStartPartSpawner created. Assign part prefabs and spawn points in its Inspector.");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    static T EnsureManager<T>(string name) where T : Component
    {
        T existing = Object.FindFirstObjectByType<T>();
        if (existing != null) return existing;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go.AddComponent<T>();
    }

    static GameObject FindExisting(string name)
    {
        GameObject found = GameObject.Find(name);
        if (found != null)
        {
            Debug.Log($"[SceneSetupWizard] '{name}' already exists — skipping.");
            return found;
        }
        return null;
    }

    static Material CreateColorMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        return mat;
    }

    static GameObject CreateScreenSpaceCanvas(string name, Transform parent)
    {
        GameObject canvasObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(canvasObject, $"Create {name}");
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject;
    }

    static GameObject CreateFillPanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panelObject, $"Create {name}");
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return panelObject;
    }
}

// Helper to allow SerializedProperty to accept object values cleanly
internal static class SerializedPropertyExtensions
{
    public static void SetValue(this SerializedProperty property, object value)
    {
        if (property == null) return;
        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = value as Object;
                break;
        }
    }
}
#endif
