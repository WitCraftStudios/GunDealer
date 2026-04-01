using UnityEngine;

public static class GameFeedback
{
    public static void Show(string message, float duration = 2.5f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        Debug.Log(message);
        GameplayUIManager.Instance.ShowToast(message, duration);
    }

    public static void Warn(string message, float duration = 2.8f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        Debug.LogWarning(message);
        GameplayUIManager.Instance.ShowToast(message, duration);
    }

    public static void Error(string message, float duration = 3.2f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        Debug.LogError(message);
        GameplayUIManager.Instance.ShowToast(message, duration);
    }
}
