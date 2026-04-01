using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SummaryOverlayManager : MonoBehaviour
{
    private static SummaryOverlayManager instance;

    [Header("UI")]
    public Canvas targetCanvas;
    public Image panel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public float defaultDisplayDuration = 4f;

    private Coroutine hideRoutine;

    public static bool HasLiveInstance => instance != null || FindFirstObjectByType<SummaryOverlayManager>() != null;

    public static SummaryOverlayManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SummaryOverlayManager>();
            }

            if (instance == null)
            {
                GameObject managerObject = new GameObject("SummaryOverlayManager");
                instance = managerObject.AddComponent<SummaryOverlayManager>();
            }

            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Duplicate SummaryOverlayManager found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        instance = this;
    }

    public void ShowSummary(string title, string body, float duration = -1f)
    {
        EnsureOverlay();

        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
        if (panel != null) panel.gameObject.SetActive(true);

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (duration == 0f)
        {
            return;
        }

        float displayDuration = duration > 0f ? duration : defaultDisplayDuration;
        hideRoutine = StartCoroutine(HideAfterDelay(displayDuration));
    }

    public void HideSummary()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
    }

    IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (panel != null) panel.gameObject.SetActive(false);
        hideRoutine = null;
    }

    void EnsureOverlay()
    {
        if (panel != null && titleText != null && bodyText != null) return;

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInChildren<Canvas>(true);
        }

        if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            targetCanvas = null;
        }

        if (targetCanvas == null)
        {
            GameObject canvasObject = new GameObject("SummaryCanvas");
            canvasObject.transform.SetParent(transform, false);
            targetCanvas = canvasObject.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject panelObject = new GameObject("SummaryOverlay");
        panelObject.transform.SetParent(targetCanvas.transform, false);

        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(700f, 240f);
        panelRect.anchoredPosition = Vector2.zero;

        panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);

        titleText = CreateText("Title", panelRect, new Vector2(0f, 52f), 36f, FontStyles.Bold);
        bodyText = CreateText("Body", panelRect, new Vector2(0f, -20f), 24f, FontStyles.Normal);
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.rectTransform.sizeDelta = new Vector2(620f, 120f);

        panelObject.SetActive(false);
    }

    TextMeshProUGUI CreateText(string name, RectTransform parent, Vector2 anchoredPosition, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(620f, 60f);
        textRect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = RuntimeUiFactory.ResolveFontAsset();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }
}
