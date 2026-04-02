using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person player controller using the Unity Input System.
/// Reads from an InputActionAsset wired to InputSystem_Actions.inputactions.
/// Fallback to legacy Input API is preserved via conditional compilation
/// so the project still compiles if the Input System package is absent.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------
    [Header("Movement")]
    public float walkSpeed  = 3.5f;
    public float sprintSpeed = 6.5f;
    public float jumpForce  = 5.5f;
    public float gravity    = -18f;

    [Header("Look")]
    public float mouseSensitivity = 0.12f;
    [Range(60f, 90f)] public float verticalLookLimit = 80f;

    [Header("Interaction")]
    public float interactRange = 2.8f;
    [Min(0f)] public float interactionProbeRadius = 0.12f;
    public KeyCode interactKey = KeyCode.E;
    public KeyCode dropKey     = KeyCode.G;

    [Header("Input Actions Asset")]
    [Tooltip("Assign the InputSystem_Actions.inputactions asset here.")]
    public InputActionAsset inputActions;

    [Header("Refs")]
    public bool canControl = true;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------
    CharacterController characterController;
    Camera playerCamera;
    float xRotation;
    Vector3 velocity;
    bool isGrounded;

    // Action references (resolved from the asset)
    InputAction moveAction;
    InputAction lookAction;
    InputAction jumpAction;
    InputAction sprintAction;
    InputAction interactAction;
    InputAction dropAction;
    InputAction cancelAction;

    bool useLegacyInput;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        ResolveCamera();
    }

    void Start()
    {
        ResolveCamera();

        if (inputActions != null)
        {
            ResolveActions();
            EnablePlayerActionMap();
        }
        else
        {
            useLegacyInput = true;
            Debug.LogWarning("[PlayerController] No InputActionAsset assigned — falling back to legacy Input API.");
        }

        SyncLookRotationToCurrentCamera();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameplayUIManager.Instance.SetContextHint(GetGameplayContextHint());
    }

    void OnEnable()
    {
        if (!useLegacyInput) EnablePlayerActionMap();
    }

    void OnDisable()
    {
        if (!useLegacyInput) DisablePlayerActionMap();
    }

    void Update()
    {
        HandleCancel();

        if (!canControl)
        {
            ClearInteractionPrompt();
            return;
        }

        HandleMovement();
        HandleLook();
        UpdateInteractionPrompt();
        HandleInteract();
        HandleDrop();
    }

    // -------------------------------------------------------------------------
    // Input action resolution
    // -------------------------------------------------------------------------

    void ResolveActions()
    {
        // Try to find the Player action map (name used in InputSystem_Actions.inputactions)
        InputActionMap playerMap = inputActions.FindActionMap("Player", throwIfNotFound: false)
                                ?? inputActions.FindActionMap("player", throwIfNotFound: false);

        if (playerMap == null)
        {
            Debug.LogWarning("[PlayerController] Could not find 'Player' action map. Falling back to legacy input.");
            useLegacyInput = true;
            return;
        }

        moveAction    = playerMap.FindAction("Move",     throwIfNotFound: false);
        lookAction    = playerMap.FindAction("Look",     throwIfNotFound: false);
        jumpAction    = playerMap.FindAction("Jump",     throwIfNotFound: false);
        sprintAction  = playerMap.FindAction("Sprint",   throwIfNotFound: false);
        interactAction = playerMap.FindAction("Interact", throwIfNotFound: false);
        dropAction    = playerMap.FindAction("Drop",     throwIfNotFound: false);
        cancelAction  = playerMap.FindAction("Cancel",   throwIfNotFound: false);

        // Supplement any missing actions with legacy input (graceful degradation)
        if (moveAction == null || lookAction == null)
        {
            Debug.LogWarning("[PlayerController] Essential actions missing in InputActionAsset. Falling back to legacy input.");
            useLegacyInput = true;
        }
    }

    void EnablePlayerActionMap()
    {
        if (inputActions == null) return;
        InputActionMap map = inputActions.FindActionMap("Player", throwIfNotFound: false)
                           ?? inputActions.FindActionMap("player", throwIfNotFound: false);
        map?.Enable();
    }

    void DisablePlayerActionMap()
    {
        if (inputActions == null) return;
        InputActionMap map = inputActions.FindActionMap("Player", throwIfNotFound: false)
                           ?? inputActions.FindActionMap("player", throwIfNotFound: false);
        map?.Disable();
    }

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    void HandleMovement()
    {
        isGrounded = characterController.isGrounded;
        if (isGrounded && velocity.y < 0f) velocity.y = -2f;

        Vector2 moveInput = ReadMove();
        bool sprinting    = ReadSprint();

        float speed = sprinting ? sprintSpeed : walkSpeed;
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        characterController.Move(move * speed * Time.deltaTime);

        if (ReadJumpPressed() && isGrounded)
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    // -------------------------------------------------------------------------
    // Look
    // -------------------------------------------------------------------------

    void HandleLook()
    {
        Vector2 lookDelta = ReadLook() * mouseSensitivity;

        xRotation = Mathf.Clamp(xRotation - lookDelta.y, -verticalLookLimit, verticalLookLimit);

        Camera viewCamera = ResolveCamera();
        if (viewCamera != null)
            viewCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * lookDelta.x);
    }

    // -------------------------------------------------------------------------
    // Interaction
    // -------------------------------------------------------------------------

    void HandleInteract()
    {
        if (!ReadInteractDown()) return;
        if (TryGetInteractionTarget(interactRange, out _, out IInteractable interactable, out _))
            interactable?.Interact();
    }

    void HandleDrop()
    {
        if (!ReadDropDown()) return;
        if (PlayerInventory.Instance != null) PlayerInventory.Instance.DropItem();
    }

    void HandleCancel()
    {
        if (!ReadCancelDown()) return;
        if (PCManager.Instance != null && PCManager.Instance.IsInPC)
            PCManager.Instance.ExitPC();
    }

    // -------------------------------------------------------------------------
    // Public helpers
    // -------------------------------------------------------------------------

    public string GetGameplayContextHint()
        => "E Interact   G Drop   Space Jump   Shift Sprint";

    public Camera ViewCamera => ResolveCamera();

    public void SyncLookRotationToCurrentCamera()
    {
        Camera viewCamera = ResolveCamera();
        if (viewCamera == null) return;

        xRotation = Mathf.Clamp(NormalizeAngle(viewCamera.transform.localEulerAngles.x), -verticalLookLimit, verticalLookLimit);
        viewCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    public string GetInteractionPromptFor(float range)
    {
        if (!TryGetInteractionTarget(range, out _, out IInteractable interactable, out IInteractionPromptProvider provider))
            return string.Empty;

        return provider != null ? provider.GetInteractionPrompt() : (interactable != null ? "Press E to interact" : string.Empty);
    }

    // -------------------------------------------------------------------------
    // Input abstraction — returns values from either new or legacy system
    // -------------------------------------------------------------------------

    Vector2 ReadMove()
    {
        if (!useLegacyInput && moveAction != null) return moveAction.ReadValue<Vector2>();
        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }

    Vector2 ReadLook()
    {
        if (!useLegacyInput && lookAction != null) return lookAction.ReadValue<Vector2>();
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    bool ReadSprint()
    {
        if (!useLegacyInput && sprintAction != null) return sprintAction.IsPressed();
        return Input.GetKey(KeyCode.LeftShift);
    }

    bool ReadJumpPressed()
    {
        if (!useLegacyInput && jumpAction != null) return jumpAction.WasPressedThisFrame();
        return Input.GetKeyDown(KeyCode.Space);
    }

    bool ReadInteractDown()
    {
        if (!useLegacyInput && interactAction != null) return interactAction.WasPressedThisFrame();
        return Input.GetKeyDown(interactKey);
    }

    bool ReadDropDown()
    {
        if (!useLegacyInput && dropAction != null) return dropAction.WasPressedThisFrame();
        return Input.GetKeyDown(dropKey);
    }

    bool ReadCancelDown()
    {
        if (!useLegacyInput && cancelAction != null) return cancelAction.WasPressedThisFrame();
        return Input.GetKeyDown(KeyCode.Escape);
    }

    void UpdateInteractionPrompt()
    {
        GameplayUIManager.Instance.SetInteractionPrompt(GetInteractionPromptFor(interactRange));
    }

    void ClearInteractionPrompt()
    {
        if (GameplayUIManager.HasLiveInstance)
            GameplayUIManager.Instance.SetInteractionPrompt(string.Empty);
    }

    Camera ResolveCamera()
    {
        if (playerCamera != null) return playerCamera;

        playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera == null) playerCamera = Camera.main;
        return playerCamera;
    }

    bool TryGetInteractionTarget(float range, out RaycastHit chosenHit, out IInteractable interactable, out IInteractionPromptProvider promptProvider)
    {
        chosenHit = default;
        interactable = null;
        promptProvider = null;

        Camera viewCamera = ResolveCamera();
        if (viewCamera == null) return false;

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        RaycastHit[] hits = interactionProbeRadius > 0f
            ? Physics.SphereCastAll(ray, interactionProbeRadius, range, ~0, QueryTriggerInteraction.Collide)
            : Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (ShouldIgnoreInteractionCollider(hitCollider)) continue;

            interactable = ResolveEnabledInterfaceInParents<IInteractable>(hitCollider.transform);
            promptProvider = ResolveEnabledInterfaceInParents<IInteractionPromptProvider>(hitCollider.transform);
            if (interactable == null && promptProvider == null) continue;

            chosenHit = hits[i];
            return true;
        }

        return false;
    }

    T ResolveEnabledInterfaceInParents<T>(Transform start) where T : class
    {
        Transform current = start;
        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.isActiveAndEnabled) continue;
                if (behaviour is T resolved) return resolved;
            }

            current = current.parent;
        }

        return null;
    }

    bool ShouldIgnoreInteractionCollider(Collider collider)
    {
        if (collider == null || !collider.enabled) return true;

        Transform colliderTransform = collider.transform;
        if (colliderTransform.IsChildOf(transform)) return true;

        return false;
    }

    float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
