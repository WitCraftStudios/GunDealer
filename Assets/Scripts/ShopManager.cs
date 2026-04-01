using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    public ShopItem[] catalog; // Drag ShopItems here
    public bool includeResourceCatalog = false;
    public string resourceCatalogPath = "Shop";

    RectTransform runtimeRoot;
    RectTransform viewportRoot;
    RectTransform listRoot;
    TextMeshProUGUI summaryText;
    ScrollRect shopScrollRect;
    readonly List<GameObject> runtimeRows = new List<GameObject>();

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
        if (item == null || item.prefab == null)
        {
            GameFeedback.Warn("That shop item is invalid.");
            RefreshShopUI();
            return;
        }

        if (CampaignManager.Instance.IsGameOver)
        {
            GameFeedback.Show("The suppliers have gone dark. Restart the campaign to buy again.");
            RefreshShopUI();
            return;
        }

        int currentDay = DayManager.HasLiveInstance ? DayManager.Instance.CurrentDay : 1;
        if (item.unlockDay > currentDay)
        {
            GameFeedback.Show($"{item.itemName} is not available until day {item.unlockDay}.", 2f);
            RefreshShopUI();
            return;
        }

        if (RewardManager.Instance == null || DeliveryZone.Instance == null)
        {
            GameFeedback.Error("The shop is missing its delivery setup.");
            RefreshShopUI();
            return;
        }

        if (RewardManager.Instance.TrySpendCash(item.cost))
        {
            DeliveryZone.Instance.SpawnItem(item);
            GameFeedback.Show($"Purchased {item.itemName} for ${item.cost}. Check the storage area.");
        }
        else
        {
            GameFeedback.Show($"Not enough cash for {item.itemName}.", 1.9f);
        }

        RefreshShopUI();
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
        int currentCash = RewardManager.Instance != null ? RewardManager.Instance.CurrentCash : 0;
        bool campaignClosed = CampaignManager.Instance.IsGameOver;

        summaryText.text = campaignClosed
            ? "Suppliers are offline. Restart the campaign to place new orders."
            : "Order parts and cases to the storage shelves.\nLocked items unlock on later days.";

        for (int i = 0; i < items.Count; i++)
        {
            CreateShopRow(items[i], currentDay, currentCash, campaignClosed);
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

    void CreateShopRow(ShopItem item, int currentDay, int currentCash, bool campaignClosed)
    {
        if (item == null) return;

        bool unlocked = item.unlockDay <= currentDay;
        bool affordable = currentCash >= item.cost;
        bool available = unlocked && affordable && !campaignClosed;

        string statusLine;
        if (campaignClosed)
        {
            statusLine = "Suppliers offline until the campaign restarts.";
        }
        else if (!unlocked)
        {
            statusLine = $"Available on day {item.unlockDay}.";
        }
        else if (!affordable)
        {
            statusLine = $"Need ${item.cost - currentCash} more cash.";
        }
        else
        {
            statusLine = "Ready for delivery to the warehouse.";
        }

        string description = string.IsNullOrWhiteSpace(item.description) ? "No supplier notes." : item.description.Trim();
        Color backgroundColor = available
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

        button.interactable = available;

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
        priceText.color = available ? new Color(0.86f, 1f, 0.88f, 1f) : new Color(0.96f, 0.84f, 0.84f, 1f);

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
        statusText.color = available
            ? new Color(0.82f, 1f, 0.86f, 1f)
            : unlocked
                ? new Color(1f, 0.86f, 0.82f, 1f)
                : new Color(0.84f, 0.88f, 0.93f, 1f);

        runtimeRows.Add(button.gameObject);
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
