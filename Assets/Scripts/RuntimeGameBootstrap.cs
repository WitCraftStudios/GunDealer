using UnityEngine;
using UnityEngine.UI;

public static class RuntimeGameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrapAfterSceneLoad()
    {
        EnsureCoreSystems();
    }

    public static void EnsureCoreSystems()
    {
        CampaignManager.Instance.GetHashCode();
        HeatManager.Instance.GetHashCode();
        DayManager.Instance.GetHashCode();
        SummaryOverlayManager.Instance.GetHashCode();
        GameplayUIManager.Instance.GetHashCode();

        ConfigureOverlayCanvases();
    }

    static void ConfigureOverlayCanvases()
    {
        CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler scaler = scalers[i];
            if (scaler == null) continue;

            Canvas canvas = scaler.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
