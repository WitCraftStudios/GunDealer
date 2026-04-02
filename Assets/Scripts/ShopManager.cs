using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    [Header("Supplier Catalog")]
    public ShopItem[] catalog; // Drag ShopItems here
    public bool includeResourceCatalog = false;
    public string resourceCatalogPath = "Shop";

    [Header("World Shop")]
    public Transform stockSpawnRoot;
    [Min(1)] public int stockColumns = 3;
    public float stockSpacingX = 1.2f;
    public float stockSpacingZ = 1.15f;
    public Vector3 stockStartOffset = new Vector3(0f, 0.05f, 0f);

    RectTransform runtimeRoot;
    RectTransform viewportRoot;
    RectTransform listRoot;
    TextMeshProUGUI summaryText;
    ScrollRect shopScrollRect;
    readonly List<GameObject> runtimeRows = new List<GameObject>();
    readonly Dictionary<ShopItem, List<ShopStockItem>> stockedItems = new Dictionary<ShopItem, List<ShopStockItem>>();
    Transform runtimeStockRoot;
    int nextStockSlotIndex;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate ShopManager found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        RuntimeGameBootstrap.EnsureCoreSystems();
        Instance = this;
    }

    public void PurchaseItem(ShopItem item)
    {
        if (!CanOrderItem(item, true))
        {
            return;
        }

        ShopStockItem stockItem = SpawnStockedItem(item);
        if (stockItem == null)
        {
            GameFeedback.Error("The shop could not place that order in the scene.");
        }
        else
        {
            GameFeedback.Show($"Ordered {item.itemName} to the shop. Head over there to buy it.", 2.6f);
        }

        RefreshShopUI();
    }

    public bool TryPurchaseWorldStock(ShopStockItem stockItem)
    {
        if (stockItem == null || stockItem.Item == null)
        {
            GameFeedback.Warn("That shop item is missing its supplier data.");
            return false;
        }

        ShopItem item = stockItem.Item;
        if (!CanOrderItem(item, false)) return false;

        if (RewardManager.Instance == null)
        {
            GameFeedback.Error("The cash system is missing.");
            return false;
        }

        if (!RewardManager.Instance.TrySpendCash(item.cost))
        {
            GameFeedback.Show($"Not enough cash for {item.itemName}.", 1.9f);
            RefreshShopUI();
            return false;
        }

        UnregisterStock(stockItem);
        stockItem.CompletePurchase();
        stockItem.TryAutoCollect();
        GameFeedback.Show($"Purchased {item.itemName} for ${item.cost}.", 2.2f);
        RefreshShopUI();
        return true;
    }

    public void RefreshShopUI()
    {
        if (PCManager.Instance == null || PCManager.Instance.shopTab == null)
        {
            return;
        }

        EnsureRuntimeShopUI();
        if (runtimeRoot == null || listRoot == null || summaryText == null)
        {
            return;
        }

        ClearRuntimeRows();

        List<ShopItem> items = GetCombinedCatalog();
        items.Sort((left, right) =>
        {
            int unlockCompare = left.unlockDay.CompareTo(right.unlockDay);
            if (unlockCompare != 0) return unlockCompare;

            int costCompare = left.cost.CompareTo(right.cost);
            if (costCompare != 0) return costCompare;

            return string.Compare(left.itemName, right.itemName, System.StringComparison.OrdinalIgnoreCase);
        });

        int currentDay = DayManager.HasLiveInstance ? DayManager.Instance.CurrentDay : 1;
        bool campaignClosed = CampaignManager.Instance.IsGameOver;

        summaryText.text = campaignClosed
            ? "Suppliers are offline. Restart the campaign to place new shop orders."
            : "Use this terminal to order stock to the physical shop.\nThen go to the shop and press E on an item to buy it.";

        for (int i = 0; i < items.Count; i++)
        {
            CreateShopRow(items[i], currentDay, campaignClosed);
        }
    }

    void EnsureRuntimeShopUI()
    {
        RectTransform shopPanelRect = PCManager.Instance.shopTab.GetComponent<RectTransform>();
        if (shopPanelRect == null) return;

        if (runtimeRoot != null && runtimeRoot.transform.parent == shopPanelRect)
        {
            return;
        }

        for (int i = 0; i < shopPanelRect.childCount; i++)
        {
            Transform child = shopPanelRect.GetChild(i);
            child.gameObject.SetActive(false);
        }

        Image rootImage = RuntimeUiFactory.CreatePanel(
            "RuntimeShopRoot",
            shopPanelRect,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0.5f, 0.5f),
            new Color(0.06f, 0.08f, 0.1f, 0f));
        runtimeRoot = rootImage.rectTransform;
        runtimeRoot.offsetMin = Vector2.zero;
        runtimeRoot.offsetMax = new Vector2(0f, -88f);

        Image summaryBackground = RuntimeUiFactory.CreatePanel(
            "SummaryBackground",
            runtimeRoot,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -18f),
            new Vector2(-44f, 88f),
            new Vector2(0.5f, 1f),
            new Color(0.08f, 0.1f, 0.13f, 0.92f));
        summaryBackground.raycastTarget = false;
        RectTransform summaryRect = summaryBackground.rectTransform;
        summaryRect.offsetMin = new Vector2(22f, -108f);
        summaryRect.offsetMax = new Vector2(-22f, -20f);

        summaryText = RuntimeUiFactory.CreateText(
            "Summary",
            summaryRect,
            string.Empty,
            21f,
            TextAlignmentOptions.TopLeft,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0f, 1f));
        summaryText.margin = new Vector4(22f, 16f, 22f, 16f);
        summaryText.color = new Color(0.91f, 0.96f, 1f, 1f);
        summaryText.raycastTarget = false;

        Image viewportBackground = RuntimeUiFactory.CreatePanel(
            "ViewportBackground",
            runtimeRoot,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0.5f, 0.5f),
            new Color(0.08f, 0.1f, 0.13f, 0.65f));
        viewportRoot = viewportBackground.rectTransform;
        viewportRoot.offsetMin = new Vector2(22f, 22f);
        viewportRoot.offsetMax = new Vector2(-22f, -126f);
        viewportBackground.gameObject.AddComponent<RectMask2D>();

        listRoot = RuntimeUiFactory.CreateRectTransform(
            "ShopContent",
            viewportRoot,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -14f),
            new Vector2(-24f, 0f),
            new Vector2(0.5f, 1f));

        VerticalLayoutGroup layout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 12f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = listRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        shopScrollRect = runtimeRoot.gameObject.AddComponent<ScrollRect>();
        shopScrollRect.horizontal = false;
        shopScrollRect.vertical = true;
        shopScrollRect.viewport = viewportRoot;
        shopScrollRect.content = listRoot;
        shopScrollRect.movementType = ScrollRect.MovementType.Clamped;
        shopScrollRect.scrollSensitivity = 30f;
    }

    List<ShopItem> GetCombinedCatalog()
    {
        List<ShopItem> combinedItems = new List<ShopItem>();
        HashSet<ShopItem> seenItems = new HashSet<ShopItem>();

        AddCatalogItems(catalog, combinedItems, seenItems);

        if (includeResourceCatalog)
        {
            ShopItem[] resourceItems = Resources.LoadAll<ShopItem>(resourceCatalogPath);
            AddCatalogItems(resourceItems, combinedItems, seenItems);
        }

        return combinedItems;
    }

    void AddCatalogItems(ShopItem[] items, List<ShopItem> destination, HashSet<ShopItem> seenItems)
    {
        if (items == null) return;

        for (int i = 0; i < items.Length; i++)
        {
            ShopItem item = items[i];
            if (item == null || seenItems.Contains(item)) continue;

            seenItems.Add(item);
            destination.Add(item);
        }
    }

    void CreateShopRow(ShopItem item, int currentDay, bool campaignClosed)
    {
        if (item == null) return;

        bool unlocked = item.unlockDay <= currentDay;
        bool orderable = unlocked && !campaignClosed;
        int stockedCount = GetStockCount(item);

        string statusLine;
        if (campaignClosed)
        {
            statusLine = "Suppliers offline until the campaign restarts.";
        }
        else if (!unlocked)
        {
            statusLine = $"Supplier unlocks on day {item.unlockDay}.";
        }
        else if (stockedCount > 0)
        {
            statusLine = $"{stockedCount} waiting in the shop. Buy there for ${item.cost}.";
        }
        else
        {
            statusLine = $"Order to the shop, then buy there for ${item.cost}.";
        }

        string description = string.IsNullOrWhiteSpace(item.description) ? "No supplier notes." : item.description.Trim();
        Color backgroundColor = orderable
            ? new Color(0.16f, 0.31f, 0.23f, 0.95f)
            : unlocked
                ? new Color(0.36f, 0.21f, 0.19f, 0.95f)
                : new Color(0.22f, 0.24f, 0.28f, 0.95f);

        Button button = RuntimeUiFactory.CreateButton(
            $"{item.name}_Row",
            listRoot,
            string.Empty,
            () => PurchaseItem(item),
            backgroundColor,
            Color.white);

        button.interactable = orderable;

        LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 126f;

        TextMeshProUGUI defaultLabel = button.GetComponentInChildren<TextMeshProUGUI>();
        if (defaultLabel != null)
        {
            Destroy(defaultLabel.gameObject);
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.sizeDelta = new Vector2(0f, 126f);
        }

        TextMeshProUGUI titleText = RuntimeUiFactory.CreateText(
            "Title",
            button.transform,
            item.itemName,
            24f,
            TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -10f),
            new Vector2(-180f, 32f),
            new Vector2(0f, 1f));
        titleText.margin = new Vector4(18f, 0f, 18f, 0f);
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;

        TextMeshProUGUI priceText = RuntimeUiFactory.CreateText(
            "Price",
            button.transform,
            $"${item.cost}",
            22f,
            TextAlignmentOptions.TopRight,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-18f, -12f),
            new Vector2(150f, 28f),
            new Vector2(1f, 1f));
        priceText.color = orderable ? new Color(0.86f, 1f, 0.88f, 1f) : new Color(0.96f, 0.84f, 0.84f, 1f);

        TextMeshProUGUI descriptionText = RuntimeUiFactory.CreateText(
            "Description",
            button.transform,
            description,
            18f,
            TextAlignmentOptions.TopLeft,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, -46f),
            new Vector2(-36f, -76f),
            new Vector2(0f, 1f));
        descriptionText.margin = new Vector4(18f, 0f, 18f, 0f);
        descriptionText.color = new Color(0.89f, 0.93f, 0.97f, 0.96f);
        descriptionText.fontSize = 18f;
        descriptionText.textWrappingMode = TextWrappingModes.Normal;

        TextMeshProUGUI statusText = RuntimeUiFactory.CreateText(
            "Status",
            button.transform,
            statusLine,
            18f,
            TextAlignmentOptions.BottomLeft,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 10f),
            new Vector2(-36f, 24f),
            new Vector2(0f, 0f));
        statusText.margin = new Vector4(18f, 0f, 18f, 0f);
        statusText.color = orderable
            ? new Color(0.82f, 1f, 0.86f, 1f)
            : unlocked
                ? new Color(1f, 0.86f, 0.82f, 1f)
                : new Color(0.84f, 0.88f, 0.93f, 1f);

        runtimeRows.Add(button.gameObject);
    }

    bool CanOrderItem(ShopItem item, bool refreshUiOnFailure)
    {
        if (item == null || item.prefab == null)
        {
            GameFeedback.Warn("That shop item is invalid.");
            if (refreshUiOnFailure) RefreshShopUI();
            return false;
        }

        if (CampaignManager.Instance.IsGameOver)
        {
            GameFeedback.Show("The suppliers have gone dark. Restart the campaign to trade again.");
            if (refreshUiOnFailure) RefreshShopUI();
            return false;
        }

        int currentDay = DayManager.HasLiveInstance ? DayManager.Instance.CurrentDay : 1;
        if (item.unlockDay > currentDay)
        {
            GameFeedback.Show($"{item.itemName} is not available until day {item.unlockDay}.", 2f);
            if (refreshUiOnFailure) RefreshShopUI();
            return false;
        }

        return true;
    }

    ShopStockItem SpawnStockedItem(ShopItem item)
    {
        Transform stockRoot = EnsureRuntimeStockRoot();
        if (stockRoot == null) return null;

        int slotIndex = nextStockSlotIndex++;
        Vector3 spawnPosition = GetStockPosition(slotIndex);
        GameObject spawned = Instantiate(item.prefab, spawnPosition, stockRoot.rotation);
        ShopStockItem stockItem = spawned.GetComponent<ShopStockItem>();
        if (stockItem == null) stockItem = spawned.AddComponent<ShopStockItem>();
        stockItem.Initialize(this, item);

        RegisterStock(item, stockItem);
        return stockItem;
    }

    void RegisterStock(ShopItem item, ShopStockItem stockItem)
    {
        if (item == null || stockItem == null) return;

        if (!stockedItems.TryGetValue(item, out List<ShopStockItem> stockList))
        {
            stockList = new List<ShopStockItem>();
            stockedItems[item] = stockList;
        }

        stockList.Add(stockItem);
    }

    void UnregisterStock(ShopStockItem stockItem)
    {
        if (stockItem == null || stockItem.Item == null) return;
        if (!stockedItems.TryGetValue(stockItem.Item, out List<ShopStockItem> stockList)) return;

        stockList.Remove(stockItem);
    }

    int GetStockCount(ShopItem item)
    {
        if (item == null) return 0;
        if (!stockedItems.TryGetValue(item, out List<ShopStockItem> stockList)) return 0;

        int count = 0;
        for (int i = stockList.Count - 1; i >= 0; i--)
        {
            ShopStockItem stockItem = stockList[i];
            if (stockItem == null)
            {
                stockList.RemoveAt(i);
                continue;
            }

            if (!stockItem.IsPurchased) count++;
        }

        return count;
    }

    Transform EnsureRuntimeStockRoot()
    {
        Transform anchor = ResolveStockAnchor();
        if (anchor == null) return null;

        if (runtimeStockRoot != null && runtimeStockRoot.parent == anchor) return runtimeStockRoot;

        Transform existing = anchor.Find("RuntimeShopStock");
        if (existing != null)
        {
            runtimeStockRoot = existing;
            return runtimeStockRoot;
        }

        GameObject root = new GameObject("RuntimeShopStock");
        runtimeStockRoot = root.transform;
        runtimeStockRoot.SetParent(anchor, false);
        runtimeStockRoot.localPosition = Vector3.zero;
        runtimeStockRoot.localRotation = Quaternion.identity;
        return runtimeStockRoot;
    }

    Transform ResolveStockAnchor()
    {
        if (stockSpawnRoot != null) return stockSpawnRoot;

        string[] candidateNames =
        {
            "ShopStockRoot",
            "ShopCounter",
            "ShopFloor",
            "Shop"
        };

        for (int i = 0; i < candidateNames.Length; i++)
        {
            Transform candidate = GameObject.Find(candidateNames[i])?.transform;
            if (candidate != null)
            {
                stockSpawnRoot = candidate;
                return stockSpawnRoot;
            }
        }

        stockSpawnRoot = transform;
        return stockSpawnRoot;
    }

    Vector3 GetStockPosition(int slotIndex)
    {
        Transform stockRoot = runtimeStockRoot != null ? runtimeStockRoot : EnsureRuntimeStockRoot();
        if (stockRoot == null) return transform.position;

        int safeColumns = Mathf.Max(1, stockColumns);
        int column = slotIndex % safeColumns;
        int row = slotIndex / safeColumns;
        float centeredColumn = column - ((safeColumns - 1) * 0.5f);
        Vector3 localOffset = stockStartOffset + new Vector3(centeredColumn * stockSpacingX, 0f, -row * stockSpacingZ);
        return stockRoot.TransformPoint(localOffset);
    }

    void ClearRuntimeRows()
    {
        for (int i = 0; i < runtimeRows.Count; i++)
        {
            if (runtimeRows[i] != null)
            {
                Destroy(runtimeRows[i]);
            }
        }

        runtimeRows.Clear();
    }
}
