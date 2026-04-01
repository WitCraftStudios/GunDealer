using System;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpHeight = 2f;
    public float mouseSensitivity = 2f;

    [Header("Ground Check")]
    public Transform groundCheck; // Empty child at bottom
    public float groundDistance = 0.2f;
    public LayerMask groundMask;

    [Header("Interaction")]
    public float interactDistance = 3f;
    public LayerMask interactLayer = 1 << 3; // Interactable layer (default)
    public KeyCode interactKey = KeyCode.E;
    public KeyCode dropKey = KeyCode.G;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode cancelKey = KeyCode.Escape;

    private Rigidbody rb;
    private Camera playerCam;
    private float xRotation = 0f;
    private bool isGrounded;
    private IInteractable focusedInteractable;
    private IInteractionPromptProvider focusedPromptProvider;

    public bool canControl = true; // Set false during UI

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        RuntimeGameBootstrap.EnsureCoreSystems();
        playerCam = Camera.main;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameplayUIManager.Instance.SetContextHint(GetGameplayContextHint());
    }

    void Update()
    {
        if (rb == null) return;

        if (playerCam == null)
        {
            playerCam = Camera.main;
        }

        HandleCancelInput();

        if (canControl)
        {
            if (playerCam == null) return;

            UpdateFocusedInteractable();

            // Mouse Look
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            playerCam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);

            // Ground Check
            if (groundCheck != null)
            {
                isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            }

            // Movement
            float speed = Input.GetKey(sprintKey) ? runSpeed : walkSpeed;
            Vector3 move = transform.right * Input.GetAxis("Horizontal") + transform.forward * Input.GetAxis("Vertical");
            rb.linearVelocity = new Vector3(move.x * speed, rb.linearVelocity.y, move.z * speed);

            // Jump
            if (Input.GetKeyDown(jumpKey) && isGrounded) rb.AddForce(Vector3.up * jumpHeight, ForceMode.Impulse);

            if (Input.GetKeyDown(dropKey))
            {
                if (PlayerInventory.Instance != null && PlayerInventory.Instance.heldItem != null)
                {
                    PlayerInventory.Instance.DropItem();
                }
                else
                {
                    GameFeedback.Show("Your hands are already empty.", 1.5f);
                }
            }

            // Interact
            if (Input.GetKeyDown(interactKey))
            {
                if (focusedInteractable != null)
                {
                    focusedInteractable.Interact();
                    UpdateFocusedInteractable();
                }
            }
        }
        else
        {
            // Zero horizontal velocity to stop momentum
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            focusedInteractable = null;
            focusedPromptProvider = null;
            GameplayUIManager.Instance.SetInteractionPrompt(string.Empty);
        }
    }

    void HandleCancelInput()
    {
        if (!Input.GetKeyDown(cancelKey)) return;

        if (PCManager.Instance != null && PCManager.Instance.IsInPC)
        {
            PCManager.Instance.ExitPC();
        }
    }

    void UpdateFocusedInteractable()
    {
        focusedInteractable = null;
        focusedPromptProvider = null;

        if (playerCam == null)
        {
            GameplayUIManager.Instance.SetInteractionPrompt(string.Empty);
            return;
        }

        Ray ray = playerCam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, interactDistance, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            IInteractable interactable = hitCollider.GetComponent<IInteractable>();
            if (interactable == null)
            {
                interactable = hitCollider.GetComponentInParent<IInteractable>();
            }

            if (interactable != null)
            {
                focusedInteractable = interactable;
                focusedPromptProvider = hitCollider.GetComponent<IInteractionPromptProvider>();
                if (focusedPromptProvider == null)
                {
                    focusedPromptProvider = hitCollider.GetComponentInParent<IInteractionPromptProvider>();
                }

                string prompt = focusedPromptProvider != null
                    ? focusedPromptProvider.GetInteractionPrompt()
                    : $"Press {GetKeyLabel(interactKey)} to interact";

                GameplayUIManager.Instance.SetInteractionPrompt(prompt);
                return;
            }

            if (!hitCollider.isTrigger)
            {
                break;
            }
        }

        GameplayUIManager.Instance.SetInteractionPrompt(string.Empty);
    }

    public string GetGameplayContextHint()
    {
        return $"{GetKeyLabel(interactKey)} Use   {GetKeyLabel(dropKey)} Drop   {GetKeyLabel(jumpKey)} Jump   {GetKeyLabel(sprintKey)} Run";
    }

    string GetKeyLabel(KeyCode key)
    {
        return key switch
        {
            KeyCode.LeftShift => "Shift",
            KeyCode.RightShift => "Shift",
            KeyCode.Space => "Space",
            KeyCode.Mouse0 => "LMB",
            KeyCode.Mouse1 => "RMB",
            _ => key.ToString().ToUpperInvariant().Replace("ALPHA", string.Empty)
        };
    }

    void OnDrawGizmos()
    {
        if (playerCam == null) playerCam = Camera.main;
        if (playerCam == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(playerCam.transform.position, playerCam.transform.forward * interactDistance);
    }
}
