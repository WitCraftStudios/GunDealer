using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance; // Singleton for easy access

    [Header("UI References")] // These will be assigned in Inspector
    public TextMeshProUGUI orderText; // The text that shows order details
    public Button acceptButton; // Accept button
    public Button rejectButton; // Reject button
    public GameObject orderUIPanel; // The whole UI panel (to show/hide)

    [Header("Orders")]
    public GunOrder[] possibleOrders; // Array—drag your 3-5 .asset files here in Inspector
    public bool includeResourceOrders = true;
    public string resourceOrdersPath = "Orders";

    [Header("Other Refs")]
    public GameObject blueprintPrefab; // Temp: a simple Cube prefab for now
    public Transform printerSpawnPoint; // Empty transform where blueprint spawns
    public AudioSource notificationSound; // Sound source for ding
    public Transform computerViewTarget; // Drag InteractMarker here in Inspector
    public PlayerController playerController; // Drag Player here in Inspector

    public GunOrder currentOrder; // Public for access from other scripts

    public float rejectWaitTime = 10f; // Seconds before new order after reject

    private Coroutine pendingOrderRoutine;
    private GameObject activeBlueprint;
    private bool hasAcceptedOrder;
    private GunOrder lastOfferedOrder;
    private RectTransform runtimeOrderRoot;
    private TextMeshProUGUI runtimeOrderText;
    private Button runtimeAcceptButton;
    private Button runtimeRejectButton;
    private TextMeshProUGUI runtimeHintText;
    private TextMeshProUGUI blueprintDisplayText;
    private RectTransform blueprintDisplayRect;

    public bool HasAcceptedOrder => hasAcceptedOrder && currentOrder != null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate OrderManager found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        Instance = this;
        if (orderUIPanel != null) orderUIPanel.SetActive(false); // Hide UI at start
    }

    void Start()
    {
        RuntimeGameBootstrap.EnsureCoreSystems();
        EnsureBlueprintDisplay();
        RefreshBlueprintDisplay();
        CampaignManager.Instance.RefreshHUD();
        DayManager.Instance.EnsureDayStarted();
    }

    // PUSH: System generates order, plays sound. NO UI/Camera change.
    public void GenerateNewOrder()
    {
        if (currentOrder != null) { Debug.Log("Order already active - can't generate new"); return; }
        List<GunOrder> orderPool = GetCombinedOrderPool();
        if (orderPool.Count == 0)
        {
            GameFeedback.Error("No orders are configured for the order board.");
            return;
        }
        if (CampaignManager.Instance.IsClosed)
        {
            RefreshOrderUI(false);
            return;
        }

        if (DayManager.HasLiveInstance && !DayManager.Instance.CanGenerateOrders)
        {
            RefreshOrderUI(false);
            return;
        }

        currentOrder = SelectNextOrder(orderPool);
        if (currentOrder == null)
        {
            GameFeedback.Error("No valid orders are available to generate.");
            RefreshOrderUI(false);
            return;
        }

        hasAcceptedOrder = false;
        RefreshOrderUI(false);

        if (notificationSound != null) notificationSound.Play(); // Notify player
        GameFeedback.Show($"New order available: {currentOrder.gunName}.", 2.1f);
    }

    public void ViewOrder()
    {
        if (PCManager.Instance != null) PCManager.Instance.EnterPC();

        RefreshOrderUI(true);
        if (currentOrder == null) GameFeedback.Show("No active orders are waiting right now.", 1.8f);
    }

    // Called when "Accept" button is clicked
    public void AcceptOrder()
    {
        if (CampaignManager.Instance.IsGameOver || CampaignManager.Instance.IsWon) return;
        if (currentOrder == null || hasAcceptedOrder) return;
        GameFeedback.Show($"Accepted order: {currentOrder.gunName}.");

        hasAcceptedOrder = true;
        SpawnBlueprint();
        if (TimerManager.Instance != null) TimerManager.Instance.StartTimer(currentOrder.timeLimit);
        HeatManager.Instance.ApplyOrderAccepted(currentOrder);
        DayManager.Instance.NotifyOrderAccepted(currentOrder);

        // Tutorial hook
        TutorialManager.FireOrderAccepted();
        ForestEncounterManager.Instance.NotifyOrderAccepted(currentOrder);

        RefreshOrderUI(true);
        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
        if (PCManager.Instance != null) PCManager.Instance.ExitPC();
    }

    public void RejectOrder()
    {
        if (currentOrder == null || hasAcceptedOrder) return;

        GunOrder rejectedOrder = currentOrder;
        bool shouldQueueNextOrder = DayManager.Instance.NotifyOrderRejected(rejectedOrder);

        GameFeedback.Show($"Rejected {rejectedOrder.gunName}.", 1.8f);
        ResetCurrentOrderState();
        if (ForestEncounterManager.HasLiveInstance) ForestEncounterManager.Instance.ClearActiveNpcs();
        if (PCManager.Instance != null) PCManager.Instance.ExitPC();
        if (shouldQueueNextOrder) QueueNextOrder();
    }

    IEnumerator WaitForNewOrder(float delay)
    {
        yield return new WaitForSeconds(delay);
        pendingOrderRoutine = null;
        if (CampaignManager.Instance.IsClosed) yield break;
        GenerateNewOrder();
    }

    public void CompleteOrder()
    {
        GunOrder completedOrder = currentOrder;
        bool shouldQueueNextOrder = completedOrder != null ? DayManager.Instance.NotifyOrderCompleted(completedOrder) : true;

        ResetCurrentOrderState();
        if (ForestEncounterManager.HasLiveInstance) ForestEncounterManager.Instance.ClearActiveNpcs();
        if (shouldQueueNextOrder) QueueNextOrder();
    }

    // Keeps the order UI in sync with current state
    public void RefreshOrderUI(bool forceVisible)
    {
        if (orderUIPanel != null && forceVisible) orderUIPanel.SetActive(true);
        EnsureRuntimeOrderUI();

        bool hasOrder = currentOrder != null;
        TextMeshProUGUI targetOrderText = runtimeOrderText != null ? runtimeOrderText : orderText;
        if (targetOrderText != null)
        {
            if (!hasOrder)
            {
                if (CampaignManager.Instance.IsGameOver)
                {
                    targetOrderText.text = "Warehouse compromised.\n\nPress R to restart the campaign.";
                }
                else if (CampaignManager.Instance.IsWon)
                {
                    targetOrderText.text = "Operation complete.\n\nPress R to start a new run.";
                }
                else if (DayManager.HasLiveInstance && DayManager.Instance.IsTransitioning)
                {
                    targetOrderText.text = "Shift is closed.\n\nWait for the next day to begin.";
                }
                else
                {
                    targetOrderText.text = "Waiting for a new order...\n\nStay near the computer and review the next buyer request when it lands.";
                }
            }
            else
            {
                string status = hasAcceptedOrder ? "Status: Accepted" : "Status: Pending";
                string notes = string.IsNullOrWhiteSpace(currentOrder.orderNotes) ? string.Empty : $"\nNotes: {currentOrder.orderNotes}";
                string requiredParts = BuildRequiredPartsText(currentOrder);
                targetOrderText.text =
                    $"Buyer: {currentOrder.buyerName}\n" +
                    $"Gun: {currentOrder.gunName}\n" +
                    $"Price: ${currentOrder.price}\n" +
                    $"Risk: {currentOrder.riskLevel}\n" +
                    $"Unlock Day: {currentOrder.unlockDay}\n" +
                    $"Time: {Mathf.RoundToInt(currentOrder.timeLimit)}s\n" +
                    $"{status}{requiredParts}{notes}";
            }
        }

        bool canRespond = hasOrder && !hasAcceptedOrder && !CampaignManager.Instance.IsClosed;
        if (acceptButton != null) acceptButton.interactable = canRespond;
        if (rejectButton != null) rejectButton.interactable = canRespond;
        if (runtimeAcceptButton != null) runtimeAcceptButton.interactable = canRespond;
        if (runtimeRejectButton != null) runtimeRejectButton.interactable = canRespond;

        if (runtimeHintText != null)
        {
            runtimeHintText.text = hasAcceptedOrder
                ? "Blueprint printed. Build the order, case it, and ship it."
                : "Review the buyer notes, then accept or reject the offer.";
        }

        RefreshBlueprintDisplay();
    }

    void SpawnBlueprint()
    {
        EnsureBlueprintDisplay();
        RefreshBlueprintDisplay();
    }

    void ClearBlueprint()
    {
        RefreshBlueprintDisplay();
    }

    void ResetCurrentOrderState()
    {
        currentOrder = null;
        hasAcceptedOrder = false;
        ClearBlueprint();
        if (TimerManager.Instance != null) TimerManager.Instance.StopTimer();
        RefreshOrderUI(false);
        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
    }

    void QueueNextOrder()
    {
        QueueNextOrder(rejectWaitTime);
    }

    public void RequestNextOrder(float delay = -1f)
    {
        QueueNextOrder(delay >= 0f ? delay : rejectWaitTime);
    }

    void QueueNextOrder(float delay)
    {
        if (CampaignManager.Instance.IsClosed) return;
        if (DayManager.HasLiveInstance && !DayManager.Instance.CanGenerateOrders) return;

        if (pendingOrderRoutine != null)
        {
            StopCoroutine(pendingOrderRoutine);
        }

        pendingOrderRoutine = StartCoroutine(WaitForNewOrder(delay));
    }

    void EnsureBlueprintDisplay()
    {
        if (blueprintPrefab == null || printerSpawnPoint == null) return;

        if (activeBlueprint == null)
        {
            activeBlueprint = Instantiate(blueprintPrefab, printerSpawnPoint.position, printerSpawnPoint.rotation);
            activeBlueprint.transform.SetParent(printerSpawnPoint, true);
        }

        if (blueprintDisplayText != null) return;

        GameObject canvasObject = new GameObject("OrderDisplayCanvas");
        canvasObject.transform.SetParent(activeBlueprint.transform, false);
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.AddComponent<BillboardToCamera>();

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 16f;

        canvasObject.AddComponent<GraphicRaycaster>();

        blueprintDisplayRect = canvas.GetComponent<RectTransform>();

        Image background = RuntimeUiFactory.CreatePanel(
            "DisplayBackground",
            canvasObject.transform,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0.5f, 0.5f),
            new Color(0.05f, 0.07f, 0.09f, 0.92f));
        background.raycastTarget = false;

        blueprintDisplayText = RuntimeUiFactory.CreateText(
            "DisplayText",
            canvasObject.transform,
            string.Empty,
            14f,
            TextAlignmentOptions.TopLeft,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0f, 1f));
        blueprintDisplayText.margin = new Vector4(8f, 8f, 8f, 8f);
        blueprintDisplayText.color = new Color(0.93f, 0.97f, 1f, 1f);
        blueprintDisplayText.textWrappingMode = TextWrappingModes.Normal;
        blueprintDisplayText.enableAutoSizing = true;
        blueprintDisplayText.fontSizeMin = 6f;
        blueprintDisplayText.fontSizeMax = 14f;
        blueprintDisplayText.overflowMode = TextOverflowModes.Truncate;
        blueprintDisplayText.raycastTarget = false;
        UpdateBlueprintDisplayLayout();
    }

    void RefreshBlueprintDisplay()
    {
        EnsureBlueprintDisplay();
        if (blueprintDisplayText == null) return;
        UpdateBlueprintDisplayLayout();

        if (CampaignManager.Instance.IsGameOver)
        {
            blueprintDisplayText.text = "COMPROMISED\nPress R";
            return;
        }

        if (CampaignManager.Instance.IsWon)
        {
            blueprintDisplayText.text = "OP COMPLETE\nPress R";
            return;
        }

        if (currentOrder == null)
        {
            blueprintDisplayText.text = "ORDER BOX\nNo active job\nUse PC";
            return;
        }

        string statusLabel = hasAcceptedOrder ? "CURRENT JOB" : "INCOMING";
        string recipeLines = BuildCompactRecipeString(currentOrder);
        blueprintDisplayText.text =
            $"{statusLabel}\n" +
            $"{currentOrder.gunName}\n" +
            $"{currentOrder.buyerName}\n" +
            $"${currentOrder.price}  {Mathf.RoundToInt(currentOrder.timeLimit)}s\n" +
            $"{recipeLines}";
    }

    void UpdateBlueprintDisplayLayout()
    {
        if (activeBlueprint == null || blueprintDisplayRect == null) return;

        BoxCollider boxCollider = activeBlueprint.GetComponent<BoxCollider>();
        Vector3 localSize = boxCollider != null ? boxCollider.size : Vector3.one;
        Vector3 localCenter = boxCollider != null ? boxCollider.center : Vector3.zero;

        blueprintDisplayRect.sizeDelta = new Vector2(
            Mathf.Max(1f, localSize.x * 100f),
            Mathf.Max(1f, localSize.z * 100f));

        Transform canvasTransform = blueprintDisplayRect.transform;
        canvasTransform.localPosition = localCenter + Vector3.up * (localSize.y * 0.5f + 0.03f);
        canvasTransform.localScale = Vector3.one * 0.01f;
    }

    public void ResetCampaignProgress()
    {
        if (pendingOrderRoutine != null)
        {
            StopCoroutine(pendingOrderRoutine);
            pendingOrderRoutine = null;
        }

        lastOfferedOrder = null;
        ResetCurrentOrderState();
        if (ForestEncounterManager.HasLiveInstance) ForestEncounterManager.Instance.ClearActiveNpcs();
    }

    public string GetObjectiveHudLine()
    {
        if (CampaignManager.Instance.IsGameOver)
        {
            return "Objective: Restart the campaign";
        }

        if (CampaignManager.Instance.IsWon)
        {
            return "Objective: Start a fresh campaign";
        }

        if (currentOrder == null)
        {
            return "Objective: Check the computer for the next order";
        }

        if (!hasAcceptedOrder)
        {
            return $"Objective: Review the {currentOrder.gunName} offer";
        }

        return $"Objective: Build and ship {currentOrder.gunName}";
    }

    public string GetRecipeHudLine()
    {
        if (currentOrder == null || !hasAcceptedOrder) return string.Empty;
        return $"Recipe: {BuildRecipeString(currentOrder, " | ")}";
    }

    string BuildRecipeString(GunOrder order, string separator)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (order.requiresGrip)    { if (sb.Length > 0) sb.Append(separator); sb.Append(GetPartTypeLabel(order.grip)); }
        if (order.requiresTrigger) { if (sb.Length > 0) sb.Append(separator); sb.Append(GetPartTypeLabel(order.trigger)); }
        if (order.requiresMag)     { if (sb.Length > 0) sb.Append(separator); sb.Append(GetPartTypeLabel(order.magazine)); }
        if (order.requiresBody)    { if (sb.Length > 0) sb.Append(separator); sb.Append(GetPartTypeLabel(order.body)); }
        return sb.Length > 0 ? sb.ToString() : "(no parts required)";
    }

    string BuildRequiredPartsText(GunOrder order)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("\nRequired Parts:");
        if (order.requiresGrip)    sb.Append($"\n- {GetPartTypeLabel(order.grip)}");
        if (order.requiresTrigger) sb.Append($"\n- {GetPartTypeLabel(order.trigger)}");
        if (order.requiresMag)     sb.Append($"\n- {GetPartTypeLabel(order.magazine)}");
        if (order.requiresBody)    sb.Append($"\n- {GetPartTypeLabel(order.body)}");
        return sb.ToString();
    }

    string BuildCompactRecipeString(GunOrder order)
    {
        if (order == null) return "(no recipe)";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (order.requiresGrip)    AppendCompactRecipeLine(sb, order.grip);
        if (order.requiresTrigger) AppendCompactRecipeLine(sb, order.trigger);
        if (order.requiresMag)     AppendCompactRecipeLine(sb, order.magazine);
        if (order.requiresBody)    AppendCompactRecipeLine(sb, order.body);
        return sb.Length > 0 ? sb.ToString() : "(no parts required)";
    }

    void AppendCompactRecipeLine(System.Text.StringBuilder sb, PartType type)
    {
        if (sb.Length > 0) sb.Append('\n');
        sb.Append(GetCompactPartTypeLabel(type));
    }

    List<GunOrder> GetCombinedOrderPool()
    {
        List<GunOrder> combinedOrders = new List<GunOrder>();
        HashSet<GunOrder> seenOrders = new HashSet<GunOrder>();

        AddOrdersToPool(possibleOrders, combinedOrders, seenOrders);

        if (includeResourceOrders)
        {
            GunOrder[] resourceOrders = Resources.LoadAll<GunOrder>(resourceOrdersPath);
            AddOrdersToPool(resourceOrders, combinedOrders, seenOrders);
        }

        return combinedOrders;
    }

    void AddOrdersToPool(GunOrder[] orders, List<GunOrder> destination, HashSet<GunOrder> seenOrders)
    {
        if (orders == null) return;

        for (int i = 0; i < orders.Length; i++)
        {
            GunOrder order = orders[i];
            if (order == null || seenOrders.Contains(order)) continue;

            seenOrders.Add(order);
            destination.Add(order);
        }
    }

    void EnsureRuntimeOrderUI()
    {
        if (orderUIPanel == null) return;

        RectTransform orderPanelRect = orderUIPanel.GetComponent<RectTransform>();
        if (orderPanelRect == null) return;

        if (runtimeOrderRoot != null && runtimeOrderRoot.transform.parent == orderPanelRect)
        {
            return;
        }

        for (int i = 0; i < orderPanelRect.childCount; i++)
        {
            Transform child = orderPanelRect.GetChild(i);
            child.gameObject.SetActive(false);
        }

        Image rootImage = RuntimeUiFactory.CreatePanel(
            "RuntimeOrderRoot",
            orderPanelRect,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0.5f, 0.5f),
            new Color(0.03f, 0.04f, 0.06f, 0f));
        rootImage.raycastTarget = false;
        runtimeOrderRoot = rootImage.rectTransform;
        runtimeOrderRoot.offsetMin = new Vector2(20f, 18f);
        runtimeOrderRoot.offsetMax = new Vector2(-20f, -92f);

        Image boardBackground = RuntimeUiFactory.CreatePanel(
            "BoardBackground",
            runtimeOrderRoot,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0.5f, 0.5f),
            new Color(0.08f, 0.1f, 0.13f, 0.95f));
        boardBackground.raycastTarget = false;
        RectTransform boardRect = boardBackground.rectTransform;
        boardRect.offsetMin = Vector2.zero;
        boardRect.offsetMax = Vector2.zero;

        TextMeshProUGUI titleText = RuntimeUiFactory.CreateText(
            "Title",
            boardRect,
            "Order Board",
            34f,
            TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -22f),
            new Vector2(-48f, 52f),
            new Vector2(0f, 1f));
        titleText.margin = new Vector4(28f, 0f, 28f, 0f);
        titleText.color = new Color(0.95f, 0.98f, 1f, 1f);
        titleText.raycastTarget = false;

        Image detailsBackground = RuntimeUiFactory.CreatePanel(
            "DetailsBackground",
            boardRect,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0.5f, 0.5f),
            new Color(0.05f, 0.07f, 0.09f, 0.94f));
        detailsBackground.raycastTarget = false;
        RectTransform detailsRect = detailsBackground.rectTransform;
        detailsRect.offsetMin = new Vector2(28f, 142f);
        detailsRect.offsetMax = new Vector2(-28f, -86f);

        runtimeOrderText = RuntimeUiFactory.CreateText(
            "RuntimeOrderText",
            detailsRect,
            string.Empty,
            22f,
            TextAlignmentOptions.TopLeft,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0f, 1f));
        runtimeOrderText.margin = new Vector4(20f, 20f, 20f, 20f);
        runtimeOrderText.color = new Color(0.9f, 0.95f, 1f, 1f);
        runtimeOrderText.textWrappingMode = TextWrappingModes.Normal;
        runtimeOrderText.overflowMode = TextOverflowModes.Overflow;
        runtimeOrderText.raycastTarget = false;

        Image footerBackground = RuntimeUiFactory.CreatePanel(
            "FooterBackground",
            boardRect,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 18f),
            new Vector2(-56f, 98f),
            new Vector2(0.5f, 0f),
            new Color(0.05f, 0.07f, 0.09f, 0.95f));
        footerBackground.raycastTarget = false;
        RectTransform footerRect = footerBackground.rectTransform;
        footerRect.offsetMin = new Vector2(28f, 24f);
        footerRect.offsetMax = new Vector2(-28f, 122f);

        runtimeHintText = RuntimeUiFactory.CreateText(
            "HintText",
            footerRect,
            "Review the buyer notes, then accept or reject the offer.",
            20f,
            TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0f, 0f),
            new Vector2(-360f, 64f),
            new Vector2(0f, 0.5f));
        runtimeHintText.margin = new Vector4(22f, 0f, 22f, 0f);
        runtimeHintText.color = new Color(0.76f, 0.84f, 0.9f, 1f);
        runtimeHintText.raycastTarget = false;

        runtimeAcceptButton = RuntimeUiFactory.CreateButton(
            "RuntimeAcceptButton",
            footerRect,
            "Accept",
            AcceptOrder,
            new Color(0.15f, 0.39f, 0.27f, 1f),
            Color.white);
        ConfigureFooterButton(runtimeAcceptButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-184f, 0f));

        runtimeRejectButton = RuntimeUiFactory.CreateButton(
            "RuntimeRejectButton",
            footerRect,
            "Reject",
            RejectOrder,
            new Color(0.43f, 0.19f, 0.17f, 1f),
            Color.white);
        ConfigureFooterButton(runtimeRejectButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-28f, 0f));
    }

    void ConfigureFooterButton(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition)
    {
        if (rect == null) return;

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(140f, 48f);
    }

    GunOrder SelectNextOrder(List<GunOrder> orderPool)
    {
        List<GunOrder> unlockedOrders = new List<GunOrder>();
        int currentDayNumber = DayManager.HasLiveInstance ? DayManager.Instance.CurrentDay : 1;
        int lowestUnlockDay = int.MaxValue;
        GunOrder fallbackOrder = null;

        for (int i = 0; i < orderPool.Count; i++)
        {
            GunOrder order = orderPool[i];
            if (order == null) continue;

            if (order.unlockDay <= currentDayNumber)
            {
                unlockedOrders.Add(order);
            }

            if (order.unlockDay < lowestUnlockDay)
            {
                lowestUnlockDay = order.unlockDay;
                fallbackOrder = order;
            }
        }

        if (unlockedOrders.Count == 0 && fallbackOrder != null)
        {
            unlockedOrders.Add(fallbackOrder);
        }

        if (unlockedOrders.Count == 0)
        {
            return null;
        }

        GunOrder selectedOrder = ChooseWeightedOrder(unlockedOrders, null);
        if (unlockedOrders.Count > 1 && selectedOrder == lastOfferedOrder)
        {
            GunOrder rerolledOrder = ChooseWeightedOrder(unlockedOrders, lastOfferedOrder);
            if (rerolledOrder != null)
            {
                selectedOrder = rerolledOrder;
            }
        }

        lastOfferedOrder = selectedOrder;
        return selectedOrder;
    }

    GunOrder ChooseWeightedOrder(List<GunOrder> orders, GunOrder excludedOrder)
    {
        int totalWeight = 0;
        for (int i = 0; i < orders.Count; i++)
        {
            GunOrder order = orders[i];
            if (order == null || order == excludedOrder) continue;
            totalWeight += Mathf.Max(1, order.selectionWeight);
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        int roll = Random.Range(0, totalWeight);
        for (int i = 0; i < orders.Count; i++)
        {
            GunOrder order = orders[i];
            if (order == null || order == excludedOrder) continue;

            roll -= Mathf.Max(1, order.selectionWeight);
            if (roll < 0)
            {
                return order;
            }
        }

        return null;
    }

    public static string GetPartTypeLabel(PartType type)
    {
        switch (type)
        {
            case PartType.GripA:
                return "Vertical Grip";
            case PartType.GripB:
                return "Horizontal Grip";
            case PartType.Trigger1:
                return "Trigger 1";
            case PartType.Trigger2:
                return "Trigger 2";
            case PartType.MagShort:
                return "Short Magazine";
            case PartType.MagLong:
                return "Long Magazine";
            case PartType.BodyAK:
                return "AK Body";
            case PartType.BodyAR:
                return "AR Body";
            case PartType.Assembled:
                return "Assembled Gun";
            default:
                return "Unknown Part";
        }
    }

    static string GetCompactPartTypeLabel(PartType type)
    {
        switch (type)
        {
            case PartType.GripA:
                return "Vert Grip";
            case PartType.GripB:
                return "Horiz Grip";
            case PartType.Trigger1:
                return "Trigger 1";
            case PartType.Trigger2:
                return "Trigger 2";
            case PartType.MagShort:
                return "Short Mag";
            case PartType.MagLong:
                return "Long Mag";
            case PartType.BodyAK:
                return "AK Body";
            case PartType.BodyAR:
                return "AR Body";
            case PartType.Assembled:
                return "Built Gun";
            default:
                return "Unknown";
        }
    }
}
