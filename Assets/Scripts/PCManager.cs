using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class PCManager : MonoBehaviour
{
    public static PCManager Instance;

    [Header("UI Refs")]
    public GameObject pcCanvas; // The main canvas for PC
    public GameObject orderTab; // Panel for Orders
    public GameObject shopTab;  // Panel for Shop

    [Header("Camera")]
    public Transform computerViewTarget;
    public PlayerController playerController;

    private Vector3 originalCamPos;
    private Quaternion originalCamRot;
    private bool isInPC = false;
    private Coroutine cameraTransition;
    private GameObject runtimeNavRoot;
    private Button orderTabButton;
    private Button shopTabButton;
    private Button closeButton;
    private TextMeshProUGUI headerText;

    public bool IsInPC => isInPC;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate PCManager found. Destroying extra instance.");
            Destroy(this);
            return;
        }

        RuntimeGameBootstrap.EnsureCoreSystems();
        Instance = this;
        CloseAllTabs();
        if (pcCanvas != null) pcCanvas.SetActive(false);
    }

    void Update()
    {
        if (!isInPC) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            OpenTab(shopTab != null && shopTab.activeSelf ? 0 : 1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            OpenTab(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            OpenTab(1);
        }
    }

    public void EnterPC()
    {
        if (isInPC) return;
        if (pcCanvas == null)
        {
            Debug.LogError("PCManager is missing its PC canvas.");
            return;
        }

        isInPC = true;
        
        pcCanvas.SetActive(true);
        EnsureRuntimeNavigation();
        OpenTab(0); // Default to Order tab
        GameplayUIManager.Instance.SetHudVisible(false);
        GameplayUIManager.Instance.SetWorldInteractionVisible(false);
        GameplayUIManager.Instance.SetInteractionPrompt(string.Empty);
        GameplayUIManager.Instance.SetContextHint("1 Orders   2 Shop   Tab Switch   Esc Close");

        if (cameraTransition != null) StopCoroutine(cameraTransition);
        cameraTransition = StartCoroutine(TransitionCameraToComputer());
    }

    public void ExitPC()
    {
        if (pcCanvas != null) pcCanvas.SetActive(false);
        if (cameraTransition != null) StopCoroutine(cameraTransition);
        cameraTransition = StartCoroutine(TransitionCameraBack());
        isInPC = false;
        GameplayUIManager.Instance.SetHudVisible(true);
        GameplayUIManager.Instance.SetWorldInteractionVisible(true);
        string gameplayHint = playerController != null
            ? playerController.GetGameplayContextHint()
            : "E Use   G Drop   Space Jump   Shift Run";
        GameplayUIManager.Instance.SetContextHint(gameplayHint);
    }
    
    // 0 = Orders, 1 = Shop
    public void OpenTab(int index)
    {
        EnsureRuntimeNavigation();
        CloseAllTabs();
        if (index == 0)
        {
            if (orderTab != null) orderTab.SetActive(true);
            if (OrderManager.Instance != null) OrderManager.Instance.RefreshOrderUI(true);
            if (headerText != null) headerText.text = "Order Board";
        }
        else if (index == 1 && shopTab != null)
        {
            shopTab.SetActive(true);
            if (ShopManager.Instance != null) ShopManager.Instance.RefreshShopUI();
            if (headerText != null) headerText.text = "Supply Terminal";
        }

        if (orderTabButton != null) orderTabButton.interactable = index != 0;
        if (shopTabButton != null) shopTabButton.interactable = index != 1;
    }

    void CloseAllTabs()
    {
        if (orderTab) orderTab.SetActive(false);
        if (shopTab) shopTab.SetActive(false);
    }

    void EnsureRuntimeNavigation()
    {
        if (pcCanvas == null) return;
        if (runtimeNavRoot != null) return;

        Image navBar = RuntimeUiFactory.CreatePanel(
            "RuntimePCNav",
            pcCanvas.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(0f, 84f),
            new Vector2(0.5f, 1f),
            new Color(0.05f, 0.07f, 0.09f, 0.92f));
        runtimeNavRoot = navBar.gameObject;

        headerText = RuntimeUiFactory.CreateText(
            "Header",
            runtimeNavRoot.transform,
            "Order Board",
            30f,
            TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(26f, 0f),
            new Vector2(380f, 54f),
            new Vector2(0f, 0.5f));
        headerText.color = new Color(0.93f, 0.97f, 1f, 1f);

        orderTabButton = CreateNavButton("OrdersButton", "Orders", new Vector2(-300f, -18f), () => OpenTab(0));
        shopTabButton = CreateNavButton("ShopButton", "Shop", new Vector2(-160f, -18f), () => OpenTab(1));
        closeButton = CreateNavButton("CloseButton", "Close", new Vector2(-20f, -18f), ExitPC);
    }

    Button CreateNavButton(string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        Button button = RuntimeUiFactory.CreateButton(
            name,
            runtimeNavRoot.transform,
            label,
            onClick,
            new Color(0.18f, 0.25f, 0.33f, 1f),
            Color.white);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(120f, 46f);
        return button;
    }

    // --- Camera Logic (Ported from OrderManager) ---

    IEnumerator TransitionCameraToComputer()
    {
        if (computerViewTarget == null) { Debug.LogError("PC Camera Target missing!"); yield break; }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("PCManager could not find the main camera.");
            cameraTransition = null;
            yield break;
        }

        if (playerController != null && playerController.canControl)
        {
            originalCamPos = mainCam.transform.position;
            originalCamRot = mainCam.transform.rotation;
            playerController.canControl = false;
        }

        Vector3 startPos = mainCam.transform.position;
        Quaternion startRot = mainCam.transform.rotation;
        float t = 0;
        float locationT = 0;
        
        while (t < 1)
        {
            t += Time.deltaTime; // 1s duration
            locationT = t; // Linear for now
            mainCam.transform.position = Vector3.Lerp(startPos, computerViewTarget.position, locationT);
            mainCam.transform.rotation = Quaternion.Lerp(startRot, computerViewTarget.rotation, locationT);
            yield return null;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cameraTransition = null;
    }

    IEnumerator TransitionCameraBack()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("PCManager could not find the main camera.");
            cameraTransition = null;
            yield break;
        }

        Vector3 startPos = mainCam.transform.position;
        Quaternion startRot = mainCam.transform.rotation;
        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime;
            mainCam.transform.position = Vector3.Lerp(startPos, originalCamPos, t);
            mainCam.transform.rotation = Quaternion.Lerp(startRot, originalCamRot, t);
            yield return null;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (playerController != null) playerController.canControl = true;
        cameraTransition = null;
    }
}
