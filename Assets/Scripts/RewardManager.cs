using TMPro;
using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance;
    const string CashKey = "Campaign.Cash";
    const string CompletedOrdersKey = "Campaign.CompletedOrders";

    public TMPro.TextMeshProUGUI cashUI;
    public int startingCash = 350;
    [Min(0)] public int minimumOrderPayout = 25;
    private int totalCash;
    private int completedOrders;

    public int CurrentCash => totalCash;
    public int CompletedOrders => completedOrders;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate RewardManager found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        Instance = this;
        RuntimeGameBootstrap.EnsureCoreSystems();

        // Apply difficulty preset
        if (DifficultyManager.HasLiveInstance)
        {
            DifficultyPreset preset = DifficultyManager.Instance.ActivePreset;
            if (preset != null)
            {
                startingCash       = preset.startingCash;
                minimumOrderPayout = preset.minimumOrderPayout;
            }
        }

        totalCash       = SaveSystem.LoadInt(CashKey, startingCash);
        completedOrders = SaveSystem.LoadInt(CompletedOrdersKey, 0);
        if (cashUI != null) cashUI.gameObject.SetActive(false);
        RefreshHUD();
    }

    public void GiveReward()
    {
        if (OrderManager.Instance == null || !OrderManager.Instance.HasAcceptedOrder || OrderManager.Instance.currentOrder == null)
        {
            Debug.LogWarning("Tried to give a reward without an accepted order.");
            return;
        }

        GunOrder order = OrderManager.Instance.currentOrder;
        int basePrice = order.price;
        float penalty = 0f;

        bool wrongParts = CraftingManager.Instance != null && !CraftingManager.Instance.correctAssembly;
        if (wrongParts) penalty += 0.35f;

        float overtimeSeconds = TimerManager.Instance != null ? TimerManager.Instance.OvertimeSeconds : 0f;
        if (overtimeSeconds > 0f)
        {
            float overtimePenalty = Mathf.Clamp01(overtimeSeconds / Mathf.Max(15f, order.timeLimit));
            penalty += overtimePenalty * 0.5f;
        }

        // Apply difficulty payout multiplier
        float payoutMultiplier = 1f;
        if (DifficultyManager.HasLiveInstance)
        {
            DifficultyPreset preset = DifficultyManager.Instance.ActivePreset;
            if (preset != null) payoutMultiplier = preset.orderPayoutMultiplier;
        }

        penalty = Mathf.Clamp01(penalty);
        int reward = Mathf.Max(
            minimumOrderPayout,
            Mathf.RoundToInt(basePrice * order.riskBonusMultiplier * payoutMultiplier * (1f - penalty)));
        totalCash += reward;
        completedOrders++;
        SaveProgress();

        if (HeatManager.Instance != null)
        {
            if (wrongParts) HeatManager.Instance.AddHeat(order.heatOnMistake, "Wrong assembly raised suspicion.");
            else HeatManager.Instance.ReduceHeat(order.heatOnCleanCompletion, "A clean delivery cooled things down.");

            if (overtimeSeconds > 0f) HeatManager.Instance.AddHeat(order.heatOnLateDelivery, "Late delivery increased heat.");
        }

        string rewardMessage = $"Delivered {order.gunName}. Payout: ${reward}.";
        if (wrongParts && overtimeSeconds > 0f) rewardMessage += " Wrong parts and overtime cut the reward.";
        else if (wrongParts) rewardMessage += " Wrong parts reduced the reward.";
        else if (overtimeSeconds > 0f) rewardMessage += " Overtime reduced the reward.";
        GameFeedback.Show(rewardMessage, 3f);

        OrderManager.Instance.CompleteOrder();
        if (CraftingManager.Instance != null)
        {
            CraftingManager.Instance.isAssembled = false;
            CraftingManager.Instance.correctAssembly = false;
        }

        // Fire tutorial event
        TutorialManager.FireGunDelivered();

        // Check win condition
        CampaignManager.Instance.CheckWinCondition(totalCash);

        RefreshHUD();
    }

    public bool TrySpendCash(int amount)
    {
        if (amount <= 0) return true;

        if (totalCash >= amount)
        {
            totalCash -= amount;
            SaveProgress();
            RefreshHUD();
            return true;
        }
        return false;
    }

    public int RemoveCash(int amount)
    {
        if (amount <= 0) return 0;

        int removed = Mathf.Min(totalCash, amount);
        totalCash -= removed;
        SaveProgress();
        RefreshHUD();
        return removed;
    }

    public int RemoveCashPercent(float percent, int minimum = 0)
    {
        if (totalCash <= 0) return 0;

        int percentageLoss = Mathf.RoundToInt(totalCash * Mathf.Clamp01(percent));
        int amount = Mathf.Max(minimum, percentageLoss);
        return RemoveCash(amount);
    }

    public void RefreshHUD()
    {
        string timerLine = "Job: Waiting";
        if (OrderManager.Instance != null && OrderManager.Instance.HasAcceptedOrder && TimerManager.Instance != null)
        {
            timerLine = TimerManager.Instance.GetHudLine();
        }
        else if (OrderManager.Instance != null && OrderManager.Instance.currentOrder != null)
        {
            timerLine = "Job: Pending";
        }

        string dayLine = DayManager.HasLiveInstance ? DayManager.Instance.GetHudDayLine() : "Day: Offline";
        string campaignLine = CampaignManager.Instance.GetHudLine();
        string alertLine = HeatManager.HasLiveInstance ? HeatManager.Instance.GetHudAlertLine() : string.Empty;
        string objectiveLine = OrderManager.Instance != null ? OrderManager.Instance.GetObjectiveHudLine() : "Objective: Check the computer";
        string recipeLine = OrderManager.Instance != null ? OrderManager.Instance.GetRecipeHudLine() : string.Empty;
        string benchLine = CraftingManager.Instance != null ? CraftingManager.Instance.GetAssemblyStatusLine() : string.Empty;
        if (string.IsNullOrWhiteSpace(alertLine) && DayManager.HasLiveInstance)
        {
            alertLine = DayManager.Instance.GetHudStatusLine();
        }

        int heatValue = HeatManager.HasLiveInstance ? HeatManager.Instance.CurrentHeat : 0;
        string hudText =
            $"Cash: ${totalCash}\n" +
            $"Heat: {heatValue}/100\n" +
            $"{campaignLine}\n" +
            $"{dayLine}\n" +
            $"{timerLine}\n" +
            $"Completed: {completedOrders}\n" +
            $"{objectiveLine}";

        if (!string.IsNullOrWhiteSpace(recipeLine))
        {
            hudText += $"\n{recipeLine}";
        }

        if (!string.IsNullOrWhiteSpace(benchLine))
        {
            hudText += $"\n{benchLine}";
        }

        if (!string.IsNullOrWhiteSpace(alertLine))
        {
            hudText += $"\nAlert: {alertLine}";
        }

        if (cashUI != null)
        {
            cashUI.text = hudText;
            cashUI.gameObject.SetActive(false);
        }

        GameplayUIManager.Instance.SetHudStatus(hudText);
    }

    public void ResetCampaignProgress()
    {
        totalCash = startingCash;
        completedOrders = 0;
        SaveProgress();
        RefreshHUD();
    }

    void SaveProgress()
    {
        SaveSystem.SaveInt(CashKey, totalCash);
        SaveSystem.SaveInt(CompletedOrdersKey, completedOrders);
    }
}
