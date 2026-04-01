using UnityEngine;

public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance;
    public float buildTimeLimit = 300f; // 5 min
    public float remainingTime;
    public bool allowOvertime = true;
    private bool isTiming;
    private bool hasExpired;

    public float OvertimeSeconds => hasExpired ? Mathf.Abs(Mathf.Min(remainingTime, 0f)) : 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate TimerManager found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        Instance = this;
    }

    public void StartTimer(float duration)
    {
        remainingTime = duration > 0f ? duration : buildTimeLimit;
        isTiming = true;
        hasExpired = false;
        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
    }

    void Update()
    {
        if (isTiming)
        {
            remainingTime -= Time.deltaTime;
            if (!hasExpired && remainingTime <= 0)
            {
                hasExpired = true;
                GameFeedback.Warn("Time is up. You are now in overtime.", 2.6f);
            }

            if (!allowOvertime && remainingTime <= 0f)
            {
                remainingTime = 0f;
                isTiming = false;
            }

            if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
        }
    }

    public void StopTimer()
    {
        isTiming = false;
        hasExpired = false;
        remainingTime = 0f;
        if (RewardManager.Instance != null) RewardManager.Instance.RefreshHUD();
    }

    public string GetHudLine()
    {
        if (!isTiming) return "Job: Active";
        if (!hasExpired) return $"Time: {Mathf.CeilToInt(remainingTime)}s";
        return $"Overtime: +{Mathf.CeilToInt(Mathf.Abs(remainingTime))}s";
    }
}
