using UnityEngine;

public class HeatManager : MonoBehaviour
{
    private static HeatManager instance;
    const string HeatKey = "Campaign.Heat";

    [Header("Heat")]
    [Range(0, 100)] public int currentHeat;
    [Range(1, 100)] public int maxHeat = 100;
    [Range(0, 100)] public int warningThreshold = 60;
    [Range(0, 100)] public int raidThreshold = 90;

    [Header("Police Pressure")]
    [Min(0)] public int inspectionBribeCost = 250;
    [Min(0)] public int inspectionHeatRelief = 15;
    [Min(0)] public int inspectionFailureHeatSpike = 10;
    [Range(0f, 1f)] public float raidCashLossPercent = 0.35f;
    [Min(0)] public int raidMinimumCashLoss = 500;
    [Range(0, 100)] public int heatAfterRaid = 45;

    private bool inspectionQueued;
    private bool raidQueued;
    private string alertMessage;
    private float alertMessageExpiry;

    public static bool HasLiveInstance => instance != null || FindFirstObjectByType<HeatManager>() != null;

    public static HeatManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<HeatManager>();
            }

            if (instance == null)
            {
                GameObject managerObject = new GameObject("HeatManager");
                instance = managerObject.AddComponent<HeatManager>();
            }

            return instance;
        }
    }

    public int CurrentHeat => currentHeat;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Duplicate HeatManager found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        instance = this;

        // Apply difficulty preset values if available
        if (DifficultyManager.HasLiveInstance)
        {
            DifficultyPreset preset = DifficultyManager.Instance.ActivePreset;
            if (preset != null)
            {
                warningThreshold   = preset.heatWarningThreshold;
                raidThreshold      = preset.heatRaidThreshold;
                inspectionBribeCost = preset.inspectionBribeCost;
            }
        }

        currentHeat = SaveSystem.LoadInt(HeatKey, currentHeat);
        inspectionQueued = currentHeat >= warningThreshold && currentHeat < raidThreshold;
        raidQueued = currentHeat >= raidThreshold;
    }

    public void ApplyOrderAccepted(GunOrder order)
    {
        if (order == null) return;

        AddHeat(order.heatOnAccept, $"Accepted a {order.riskLevel.ToString().ToLower()} risk order.");
    }

    public void ResolveQueuedPoliceAction()
    {
        if (raidQueued)
        {
            ResolveRaid();
            return;
        }

        if (inspectionQueued)
        {
            ResolveInspection();
        }
    }

    public void ApplyDayTransitionRelief(int amount)
    {
        if (amount <= 0) return;

        int previousHeat = currentHeat;
        currentHeat = Mathf.Clamp(currentHeat - amount, 0, maxHeat);
        inspectionQueued = currentHeat >= warningThreshold && currentHeat < raidThreshold;
        raidQueued = currentHeat >= raidThreshold;

        if (previousHeat != currentHeat)
        {
            SetAlert($"Night cooldown dropped heat to {currentHeat}.", 8f);
            GameFeedback.Show($"Night cooldown dropped heat to {currentHeat}.", 2.2f);
        }

        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
        SaveHeat();
    }

    public void AddHeat(int amount, string reason)
    {
        ChangeHeat(Mathf.Abs(amount), reason);
    }

    public void ReduceHeat(int amount, string reason)
    {
        ChangeHeat(-Mathf.Abs(amount), reason);
    }

    void ChangeHeat(int delta, string reason)
    {
        if (delta == 0) return;

        int previousHeat = currentHeat;
        currentHeat = Mathf.Clamp(currentHeat + delta, 0, maxHeat);
        if (currentHeat == previousHeat) return;
        bool crossedIntoWarning = previousHeat < warningThreshold && currentHeat >= warningThreshold && currentHeat < raidThreshold;
        bool crossedIntoRaid = previousHeat < raidThreshold && currentHeat >= raidThreshold;

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Debug.Log($"{reason} Heat: {previousHeat} -> {currentHeat}");
        }

        if (currentHeat >= raidThreshold)
        {
            raidQueued = true;
            inspectionQueued = false;
        }
        else if (currentHeat >= warningThreshold)
        {
            inspectionQueued = true;
            raidQueued = false;
        }
        else
        {
            inspectionQueued = false;
            raidQueued = false;
        }

        if (crossedIntoWarning)
        {
            SetAlert("Inspection flagged. Keep the next job clean or pay to stay open.");
            GameFeedback.Warn("Inspection flagged. Keep the next job clean or the heat will climb higher.", 3f);
        }

        if (crossedIntoRaid)
        {
            SetAlert("Raid team is mobilizing. The next delivery could trigger a shutdown.");
            GameFeedback.Warn("Heat is critical. The next delivery could trigger a raid.", 3.2f);
        }

        if (currentHeat < warningThreshold)
        {
            ClearAlert();
        }

        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
        SaveHeat();
    }

    public string GetHudAlertLine()
    {
        if (CampaignManager.HasLiveInstance && CampaignManager.Instance.IsGameOver)
        {
            return "Campaign lost. Press R to start over.";
        }

        if (CampaignManager.HasLiveInstance && CampaignManager.Instance.IsWon)
        {
            return "Campaign complete. Press R to start a new run.";
        }

        if (!string.IsNullOrWhiteSpace(alertMessage) && Time.time <= alertMessageExpiry)
        {
            return alertMessage;
        }

        if (raidQueued) return "Raid pressure is active.";
        if (inspectionQueued) return "Inspection pressure is active.";
        if (currentHeat >= warningThreshold) return "The police are watching.";
        return string.Empty;
    }

    void ResolveInspection()
    {
        inspectionQueued = false;

        int paid = RewardManager.Instance != null ? RewardManager.Instance.RemoveCash(inspectionBribeCost) : 0;
        if (paid >= inspectionBribeCost)
        {
            int previousHeat = currentHeat;
            currentHeat = Mathf.Clamp(currentHeat - inspectionHeatRelief, 0, maxHeat);
            SetAlert($"Inspection paid off for ${paid}. Heat cooled to {currentHeat}.", 10f);
            GameFeedback.Show($"Inspection paid off for ${paid}. Heat cooled to {currentHeat}.", 2.8f);
        }
        else
        {
            ChangeHeat(inspectionFailureHeatSpike, "You couldn't cover the inspection.");
            if (!raidQueued && currentHeat >= raidThreshold)
            {
                raidQueued = true;
            }
        }

        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
        SaveHeat();
    }

    void ResolveRaid()
    {
        raidQueued = false;
        inspectionQueued = false;

        int lostCash = RewardManager.Instance != null
            ? RewardManager.Instance.RemoveCashPercent(raidCashLossPercent, raidMinimumCashLoss)
            : 0;

        currentHeat = Mathf.Clamp(heatAfterRaid, 0, maxHeat);
        SetAlert($"Raid hit the shop. Lost ${lostCash} and the day is over.", 14f);
        GameFeedback.Warn($"Raid hit the shop. Lost ${lostCash}. Heat reset to {currentHeat}.", 3.4f);

        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
        SaveHeat();

        bool campaignEnded = CampaignManager.Instance.RegisterRaidShutdown();
        if (DayManager.HasLiveInstance)
        {
            if (campaignEnded)
            {
                DayManager.Instance.ShutdownCampaign("The cops burned the warehouse. Press R to restart.");
            }
            else
            {
                DayManager.Instance.ForceEndDayFromRaid();
            }
        }
    }

    void SetAlert(string message, float duration = 10f)
    {
        alertMessage = message;
        alertMessageExpiry = Time.time + duration;
        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
    }

    void ClearAlert()
    {
        alertMessage = string.Empty;
        alertMessageExpiry = 0f;
    }

    public void ResetCampaignProgress()
    {
        currentHeat = 0;
        inspectionQueued = false;
        raidQueued = false;
        ClearAlert();
        SaveHeat();
        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
    }

    void SaveHeat()
    {
        SaveSystem.SaveInt(HeatKey, currentHeat);
    }
}
