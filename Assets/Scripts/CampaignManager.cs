using UnityEngine;
using UnityEngine.SceneManagement;

public class CampaignManager : MonoBehaviour
{
    private static CampaignManager instance;
    const string ShutdownsKey = "Campaign.Shutdowns";
    const string GameOverKey = "Campaign.GameOver";

    [Header("Campaign")]
    [Min(1)] public int maxShutdowns = 3;
    public KeyCode restartKey = KeyCode.R;

    private int shutdownCount;
    private bool isGameOver;

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
        shutdownCount = Mathf.Clamp(PlayerPrefs.GetInt(ShutdownsKey, 0), 0, maxShutdowns);
        isGameOver = PlayerPrefs.GetInt(GameOverKey, 0) == 1;
    }

    void Start()
    {
        if (isGameOver)
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
        if (isGameOver && Input.GetKeyDown(restartKey))
        {
            StartFreshCampaign();
        }
    }

    public bool RegisterRaidShutdown()
    {
        if (isGameOver)
        {
            return true;
        }

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

    public string GetHudLine()
    {
        if (isGameOver)
        {
            return $"Campaign: Burned. Press {restartKey} to restart.";
        }

        return $"Strikes: {shutdownCount}/{maxShutdowns}";
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
        isGameOver = false;
        SaveProgress();

        if (HeatManager.HasLiveInstance) HeatManager.Instance.ResetCampaignProgress();
        if (RewardManager.Instance != null) RewardManager.Instance.ResetCampaignProgress();
        if (DayManager.HasLiveInstance) DayManager.Instance.ResetCampaignProgress();
        if (OrderManager.Instance != null) OrderManager.Instance.ResetCampaignProgress();
        if (SummaryOverlayManager.HasLiveInstance) SummaryOverlayManager.Instance.HideSummary();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            SceneManager.LoadScene(activeScene.name);
        }
    }

    void SaveProgress()
    {
        PlayerPrefs.SetInt(ShutdownsKey, shutdownCount);
        PlayerPrefs.SetInt(GameOverKey, isGameOver ? 1 : 0);
        PlayerPrefs.Save();
    }
}
