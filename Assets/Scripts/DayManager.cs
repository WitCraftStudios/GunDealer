using System.Collections;
using UnityEngine;

public class DayManager : MonoBehaviour
{
    private static DayManager instance;
    const string DayKey = "Campaign.Day";

    [Header("Day Loop")]
    [Min(1)] public int jobsPerDay = 3;
    [Min(0f)] public float firstOrderDelay = 0.5f;
    [Min(0f)] public float nextDayDelay = 5f;
    [Min(0f)] public float raidShutdownDelay = 8f;
    [Min(0)] public int overnightHeatReduction = 10;

    private int currentDay;
    private int acceptedToday;
    private int completedToday;
    private int rejectedToday;
    private bool isDayActive;
    private bool isTransitioning;
    private Coroutine nextDayRoutine;
    private string statusMessage;
    private float statusMessageExpiry;

    public static bool HasLiveInstance => instance != null || FindFirstObjectByType<DayManager>() != null;

    public static DayManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<DayManager>();
            }

            if (instance == null)
            {
                GameObject managerObject = new GameObject("DayManager");
                instance = managerObject.AddComponent<DayManager>();
            }

            return instance;
        }
    }

    public int CurrentDay => currentDay;
    public int AcceptedToday => acceptedToday;
    public int CompletedToday => completedToday;
    public int RejectedToday => rejectedToday;
    public int ResolvedJobsToday => completedToday + rejectedToday;
    public bool IsDayActive => isDayActive;
    public bool IsTransitioning => isTransitioning;
    public bool CanGenerateOrders =>
        isDayActive &&
        !isTransitioning &&
        ResolvedJobsToday < jobsPerDay &&
        (!CampaignManager.HasLiveInstance || !CampaignManager.Instance.IsClosed);

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Duplicate DayManager found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        instance = this;

        if (DifficultyManager.HasLiveInstance)
        {
            DifficultyPreset preset = DifficultyManager.Instance.ActivePreset;
            if (preset != null) jobsPerDay = preset.jobsPerDay;
        }

        currentDay = Mathf.Max(1, SaveSystem.LoadInt(DayKey, 1));
    }

    public void EnsureDayStarted()
    {
        if (CampaignManager.Instance.IsClosed) return;
        if (isDayActive || isTransitioning) return;
        StartNewDay();
    }

    public void NotifyOrderAccepted(GunOrder order)
    {
        EnsureDayStarted();
        acceptedToday++;
        SetStatusMessage($"Order accepted. {JobsRemainingForToday()} jobs left in day {currentDay}.");
        RefreshHUD();
    }

    public bool NotifyOrderRejected(GunOrder order)
    {
        rejectedToday++;
        SetStatusMessage($"{SafeOrderName(order)} was rejected.");
        return ResolveAfterOrder();
    }

    public bool NotifyOrderCompleted(GunOrder order)
    {
        completedToday++;
        SetStatusMessage($"{SafeOrderName(order)} delivered.");
        return ResolveAfterOrder();
    }

    public void ForceEndDayFromRaid()
    {
        if (isTransitioning) return;

        string summary =
            $"Day {currentDay} was shut down by a raid. Completed {completedToday}, rejected {rejectedToday}.";
        SummaryOverlayManager.Instance.ShowSummary($"Raid On Day {currentDay}", summary, Mathf.Max(4f, raidShutdownDelay - 1f));
        BeginTransition(summary, raidShutdownDelay, false);
    }

    public string GetHudDayLine()
    {
        if (currentDay <= 0) return "Day: Starting";
        if (isTransitioning) return $"Day {currentDay}: Closed";
        return $"Day {currentDay}: {ResolvedJobsToday}/{jobsPerDay}";
    }

    public string GetHudStatusLine()
    {
        if (CampaignManager.HasLiveInstance && CampaignManager.Instance.IsGameOver)
        {
            return "The warehouse is burned. Press R to restart the run.";
        }

        if (CampaignManager.HasLiveInstance && CampaignManager.Instance.IsWon)
        {
            return "The operation is complete. Press R to start a new run.";
        }

        if (!string.IsNullOrWhiteSpace(statusMessage) && Time.time <= statusMessageExpiry)
        {
            return statusMessage;
        }

        if (isTransitioning) return $"Warehouse cooling down for day {currentDay + 1}.";
        if (isDayActive) return "Keep the line moving.";
        return string.Empty;
    }

    bool ResolveAfterOrder()
    {
        if (HeatManager.HasLiveInstance)
        {
            HeatManager.Instance.ResolveQueuedPoliceAction();
        }

        if (!isDayActive || isTransitioning)
        {
            RefreshHUD();
            return false;
        }

        if (ResolvedJobsToday >= jobsPerDay)
        {
            string summary =
                $"Day {currentDay} complete. Delivered {completedToday}, rejected {rejectedToday}.";
            SummaryOverlayManager.Instance.ShowSummary($"Day {currentDay} Complete", summary, Mathf.Max(4f, nextDayDelay - 1f));
            BeginTransition(summary, nextDayDelay, true);
            return false;
        }

        RefreshHUD();
        return true;
    }

    void StartNewDay()
    {
        if (CampaignManager.Instance.IsClosed)
        {
            ShutdownCampaign(
                CampaignManager.Instance.IsWon
                    ? "Campaign complete. Press R to start a new run."
                    : "Campaign is shut down until you restart.");
            return;
        }

        if (nextDayRoutine != null)
        {
            StopCoroutine(nextDayRoutine);
            nextDayRoutine = null;
        }

        if (currentDay <= 0)
        {
            currentDay = 1;
        }

        acceptedToday = 0;
        completedToday = 0;
        rejectedToday = 0;
        isDayActive = true;
        isTransitioning = false;
        SaveProgress();
        SetStatusMessage($"Day {currentDay} begins. {jobsPerDay} jobs on the board.", 10f);
        SummaryOverlayManager.Instance.ShowSummary($"Day {currentDay}", $"{jobsPerDay} jobs are waiting. Keep the heat down.", 3.5f);
        GameFeedback.Show($"Day {currentDay} begins. {jobsPerDay} jobs are on the board.", 2.6f);
        RefreshHUD();

        if (OrderManager.Instance != null)
        {
            OrderManager.Instance.RequestNextOrder(firstOrderDelay);
        }
    }

    void BeginTransition(string summary, float delay, bool coolHeat)
    {
        isDayActive = false;
        isTransitioning = true;
        Debug.Log(summary);
        SetStatusMessage(summary, delay + 2f);

        if (coolHeat && HeatManager.HasLiveInstance)
        {
            HeatManager.Instance.ApplyDayTransitionRelief(overnightHeatReduction);
        }

        RefreshHUD();

        if (nextDayRoutine != null)
        {
            StopCoroutine(nextDayRoutine);
        }

        nextDayRoutine = StartCoroutine(StartNextDayAfterDelay(delay));
    }

    IEnumerator StartNextDayAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        nextDayRoutine = null;
        if (CampaignManager.Instance.IsClosed) yield break;
        currentDay++;
        StartNewDay();
    }

    void SetStatusMessage(string message, float duration = 8f)
    {
        statusMessage = message;
        statusMessageExpiry = Time.time + duration;
        RefreshHUD();
    }

    int JobsRemainingForToday()
    {
        return Mathf.Max(0, jobsPerDay - ResolvedJobsToday);
    }

    void RefreshHUD()
    {
        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
    }

    string SafeOrderName(GunOrder order)
    {
        if (order == null || string.IsNullOrWhiteSpace(order.gunName)) return "The order";
        return order.gunName.Trim();
    }

    public void ResetCampaignProgress()
    {
        currentDay = 1;
        acceptedToday = 0;
        completedToday = 0;
        rejectedToday = 0;
        isDayActive = false;
        isTransitioning = false;
        statusMessage = string.Empty;
        statusMessageExpiry = 0f;

        if (nextDayRoutine != null)
        {
            StopCoroutine(nextDayRoutine);
            nextDayRoutine = null;
        }

        SaveProgress();
        RefreshHUD();
    }

    public void ShutdownCampaign(string message)
    {
        isDayActive = false;
        isTransitioning = false;

        if (nextDayRoutine != null)
        {
            StopCoroutine(nextDayRoutine);
            nextDayRoutine = null;
        }

        SetStatusMessage(message, 60f);
        RefreshHUD();
    }

    void SaveProgress()
    {
        SaveSystem.SaveInt(DayKey, Mathf.Max(1, currentDay));
    }
}
