using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayUIManager : MonoBehaviour
{
    static GameplayUIManager instance;

    Canvas overlayCanvas;
    TextMeshProUGUI crosshairText;
    TextMeshProUGUI interactionPromptText;
    TextMeshProUGUI toastText;
    TextMeshProUGUI contextHintText;
    TextMeshProUGUI hudText;
    GameObject interactionPromptBackdrop;
    GameObject toastBackdrop;
    GameObject contextHintBackdrop;
    GameObject hudBackdrop;
    Coroutine toastRoutine;
    string currentInteractionPrompt = string.Empty;
    string currentHudMessage = string.Empty;
    bool hudVisible = true;
    bool worldInteractionVisible = true;

    public static bool HasLiveInstance => instance != null || FindFirstObjectByType<GameplayUIManager>() != null;

    public static GameplayUIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GameplayUIManager>();
            }

            if (instance == null)
            {
                GameObject managerObject = new GameObject("GameplayUIManager");
                instance = managerObject.AddComponent<GameplayUIManager>();
            }

            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    public void SetInteractionPrompt(string message)
    {
        EnsureOverlay();
        currentInteractionPrompt = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        ApplyInteractionVisibility();
    }

    public void SetContextHint(string message)
    {
        EnsureOverlay();

        bool hasHint = !string.IsNullOrWhiteSpace(message);
        contextHintText.gameObject.SetActive(hasHint);
        if (contextHintBackdrop != null) contextHintBackdrop.SetActive(hasHint);
        contextHintText.text = hasHint ? message : string.Empty;
    }

    public void ShowToast(string message, float duration = 2.5f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        EnsureOverlay();
        toastText.text = message.Trim();
        toastText.gameObject.SetActive(true);
        if (toastBackdrop != null) toastBackdrop.SetActive(true);

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
        }

        toastRoutine = StartCoroutine(HideToastAfterDelay(duration));
    }

    public void SetHudStatus(string message)
    {
        EnsureOverlay();
        currentHudMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        ApplyHudVisibility();
    }

    public void SetHudVisible(bool visible)
    {
        EnsureOverlay();
        hudVisible = visible;
        ApplyHudVisibility();
    }

    public void SetWorldInteractionVisible(bool visible)
    {
        EnsureOverlay();
        worldInteractionVisible = visible;
        ApplyInteractionVisibility();
    }

    IEnumerator HideToastAfterDelay(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.5f, delay));

        if (toastText != null)
        {
            toastText.gameObject.SetActive(false);
        }
        if (toastBackdrop != null)
        {
            toastBackdrop.SetActive(false);
        }

        toastRoutine = null;
    }

    void EnsureOverlay()
    {
        if (overlayCanvas != null &&
            crosshairText != null &&
            interactionPromptText != null &&
            toastText != null &&
            contextHintText != null &&
            hudText != null &&
            interactionPromptBackdrop != null &&
            toastBackdrop != null &&
            contextHintBackdrop != null &&
            hudBackdrop != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("GameplayOverlayCanvas");
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 1500;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        toastBackdrop = RuntimeUiFactory.CreatePanel(
            "ToastBackdrop",
            canvasObject.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -48f),
            new Vector2(760f, 72f),
            new Vector2(0.5f, 1f),
            new Color(0.05f, 0.08f, 0.11f, 0.78f)).gameObject;

        toastText = RuntimeUiFactory.CreateText(
            "ToastText",
            canvasObject.transform,
            string.Empty,
            28f,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -48f),
            new Vector2(700f, 60f),
            new Vector2(0.5f, 1f));
        toastText.gameObject.SetActive(false);
        toastBackdrop.SetActive(false);

        hudBackdrop = RuntimeUiFactory.CreatePanel(
            "HudBackdrop",
            canvasObject.transform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(28f, -28f),
            new Vector2(540f, 320f),
            new Vector2(0f, 1f),
            new Color(0.04f, 0.06f, 0.08f, 0.78f)).gameObject;

        hudText = RuntimeUiFactory.CreateText(
            "HudText",
            canvasObject.transform,
            string.Empty,
            21f,
            TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(38f, -36f),
            new Vector2(500f, 292f),
            new Vector2(0f, 1f));
        hudText.margin = new Vector4(18f, 16f, 18f, 16f);
        hudText.color = new Color(0.92f, 0.97f, 1f, 1f);
        hudText.textWrappingMode = TextWrappingModes.Normal;
        hudText.overflowMode = TextOverflowModes.Overflow;
        hudText.raycastTarget = false;
        hudText.gameObject.SetActive(false);
        hudBackdrop.SetActive(false);

        crosshairText = RuntimeUiFactory.CreateText(
            "Crosshair",
            canvasObject.transform,
            "+",
            42f,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(48f, 48f),
            new Vector2(0.5f, 0.5f));
        crosshairText.color = new Color(1f, 1f, 1f, 0.85f);

        interactionPromptBackdrop = RuntimeUiFactory.CreatePanel(
            "PromptBackdrop",
            canvasObject.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 32f),
            new Vector2(760f, 60f),
            new Vector2(0.5f, 0f),
            new Color(0.04f, 0.06f, 0.08f, 0.78f)).gameObject;

        interactionPromptText = RuntimeUiFactory.CreateText(
            "InteractionPrompt",
            canvasObject.transform,
            string.Empty,
            22f,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 36f),
            new Vector2(700f, 46f),
            new Vector2(0.5f, 0f));
        interactionPromptText.color = new Color(0.93f, 0.97f, 1f, 1f);
        interactionPromptText.gameObject.SetActive(false);
        interactionPromptBackdrop.SetActive(false);

        contextHintBackdrop = RuntimeUiFactory.CreatePanel(
            "ContextBackdrop",
            canvasObject.transform,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-28f, 24f),
            new Vector2(460f, 58f),
            new Vector2(1f, 0f),
            new Color(0.04f, 0.06f, 0.08f, 0.72f)).gameObject;

        contextHintText = RuntimeUiFactory.CreateText(
            "ContextHint",
            canvasObject.transform,
            string.Empty,
            18f,
            TextAlignmentOptions.MidlineRight,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-38f, 24f),
            new Vector2(420f, 48f),
            new Vector2(1f, 0f));
        contextHintText.color = new Color(0.81f, 0.88f, 0.95f, 0.95f);
        contextHintText.gameObject.SetActive(false);
        contextHintBackdrop.SetActive(false);

        ApplyHudVisibility();
        ApplyInteractionVisibility();
    }

    void ApplyHudVisibility()
    {
        bool hasHud = hudVisible && !string.IsNullOrWhiteSpace(currentHudMessage);
        if (hudText != null)
        {
            hudText.text = hasHud ? currentHudMessage : string.Empty;
            hudText.gameObject.SetActive(hasHud);
        }

        if (hudBackdrop != null)
        {
            hudBackdrop.SetActive(hasHud);
        }
    }

    void ApplyInteractionVisibility()
    {
        bool hasPrompt = worldInteractionVisible && !string.IsNullOrWhiteSpace(currentInteractionPrompt);

        if (interactionPromptText != null)
        {
            interactionPromptText.text = hasPrompt ? currentInteractionPrompt : string.Empty;
            interactionPromptText.gameObject.SetActive(hasPrompt);
        }

        if (interactionPromptBackdrop != null)
        {
            interactionPromptBackdrop.SetActive(hasPrompt);
        }

        if (crosshairText != null)
        {
            crosshairText.gameObject.SetActive(worldInteractionVisible);
            if (worldInteractionVisible)
            {
                crosshairText.color = hasPrompt ? new Color(0.58f, 0.96f, 0.76f, 1f) : new Color(1f, 1f, 1f, 0.85f);
            }
        }
    }
}
