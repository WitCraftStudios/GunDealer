using UnityEngine;
using UnityEngine.SceneManagement;

public class CampaignManager : MonoBehaviour
{
    private static CampaignManager instance;
    const string ShutdownsKey = "Campaign.Shutdowns";
    const string GameOverKey  = "Campaign.GameOver";
    const string WonKey       = "Campaign.Won";

    [Header("Campaign")]
    [Min(1)] public int maxShutdowns = 3;
    public KeyCode restartKey = KeyCode.R;

    [Header("Win Condition")]
    [Tooltip("Total cash the player must accumulate to win. 0 = disabled.")]
    [Min(0)] public int cashWinGoal = 5000;

    private int shutdownCount;
    private bool isGameOver;
    private bool isWon;

    public static bool HasLiveInstance => instance != null || FindFirstObjectByType<CampaignManager>() != null;

    public static CampaignManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<CampaignManager>();
            }

            if (instance == null)
            {
                GameObject managerObject = new GameObject("CampaignManager");
                instance = managerObject.AddComponent<CampaignManager>();
            }

            return instance;
        }
    }

    public bool IsGameOver => isGameOver;
    public bool IsWon => isWon;
    public bool IsClosed => isGameOver || isWon;
    public int ShutdownCount => shutdownCount;
    public int RemainingShutdowns => Mathf.Max(0, Mathf.Max(1, maxShutdowns) - shutdownCount);

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Duplicate CampaignManager found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        instance = this;
        maxShutdowns = Mathf.Max(1, maxShutdowns);

        // Apply difficulty preset
        if (DifficultyManager.HasLiveInstance)
        {
            DifficultyPreset preset = DifficultyManager.Instance.ActivePreset;
            if (preset != null)
            {
                maxShutdowns = Mathf.Max(1, preset.maxShutdowns);
                if (preset.cashWinGoal > 0) cashWinGoal = preset.cashWinGoal;
            }
        }

        shutdownCount = Mathf.Clamp(SaveSystem.LoadInt(ShutdownsKey, 0), 0, maxShutdowns);
        isGameOver    = SaveSystem.LoadBool(GameOverKey, false);
        isWon         = SaveSystem.LoadBool(WonKey, false);
    }

    void Start()
    {
        if (isWon)
        {
            SummaryOverlayManager.Instance.ShowSummary(
                "Operation Complete",
                $"You built the empire. The money is clean.\nPress {restartKey} to start a new run.",
                0f);
        }
        else if (isGameOver)
        {
            SummaryOverlayManager.Instance.ShowSummary(
                "Warehouse Burned",
                $"The cops shut the operation down.\nPress {restartKey} to restart the campaign.",
                0f);
        }

        RefreshHUD();
    }

    void Update()
    {
        if ((isGameOver || isWon) && Input.GetKeyDown(restartKey))
        {
            StartFreshCampaign();
        }
    }

    public bool RegisterRaidShutdown()
    {
        if (IsClosed) return true;

        shutdownCount = Mathf.Clamp(shutdownCount + 1, 0, maxShutdowns);
        isGameOver = shutdownCount >= maxShutdowns;
        SaveProgress();

        if (isGameOver)
        {
            SummaryOverlayManager.Instance.ShowSummary(
                "Warehouse Burned",
                $"The cops shut the operation down.\nPress {restartKey} to restart the campaign.",
                0f);
        }

        RefreshHUD();
        return isGameOver;
    }

    /// <summary>Called by RewardManager after each successful delivery.</summary>
    public void CheckWinCondition(int currentCash)
    {
        if (IsClosed) return;
        if (cashWinGoal <= 0) return;
        if (currentCash < cashWinGoal) return;

        isWon = true;
        SaveProgress();

        SummaryOverlayManager.Instance.ShowSummary(
            "Operation Complete",
            $"${currentCash} banked. The empire is built.\nPress {restartKey} to start a new run.",
            0f);
        GameFeedback.Show($"You reached ${cashWinGoal}. Campaign complete!", 5f);
        RefreshHUD();
    }

    public string GetHudLine()
    {
        if (isWon)     return $"Campaign: COMPLETE! Press {restartKey} to start again.";
        if (isGameOver) return $"Campaign: Burned. Press {restartKey} to restart.";

        string winProgress = cashWinGoal > 0
            ? $"  Goal: ${RewardManager.Instance?.CurrentCash ?? 0}/${cashWinGoal}"
            : string.Empty;
        return $"Strikes: {shutdownCount}/{maxShutdowns}{winProgress}";
    }

    public void RefreshHUD()
    {
        if (RewardManager.Instance != null)
        {
            RewardManager.Instance.RefreshHUD();
        }
    }

    [ContextMenu("Start Fresh Campaign")]
    public void StartFreshCampaign()
    {
        shutdownCount = 0;
        isGameOver    = false;
        isWon         = false;
        SaveProgress();

        if (HeatManager.HasLiveInstance) HeatManager.Instance.ResetCampaignProgress();
        if (RewardManager.Instance != null) RewardManager.Instance.ResetCampaignProgress();
        if (DayManager.HasLiveInstance) DayManager.Instance.ResetCampaignProgress();
        if (OrderManager.Instance != null) OrderManager.Instance.ResetCampaignProgress();
        if (SummaryOverlayManager.HasLiveInstance) SummaryOverlayManager.Instance.HideSummary();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid()) SceneManager.LoadScene(activeScene.name);
    }

    void SaveProgress()
    {
        SaveSystem.SaveInt(ShutdownsKey, shutdownCount);
        SaveSystem.SaveBool(GameOverKey, isGameOver);
        SaveSystem.SaveBool(WonKey, isWon);
    }
}
