using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Guides first-time players through the full game loop via a step-by-step tutorial.
/// Skipped automatically on subsequent runs. Reset by deleting the Tutorial.Complete save key.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    private static TutorialManager instance;
    const string TutorialCompleteKey = "Tutorial.Complete";

    // -------------------------------------------------------------------------
    // Static events — fired by game systems so TutorialManager can stay decoupled
    // -------------------------------------------------------------------------
    public static event Action OnOrderAccepted;
    public static event Action OnPartPickedUp;
    public static event Action OnPartPlacedOnBench;
    public static event Action OnGunAssembled;
    public static event Action OnGunDelivered;

    public static bool HasLiveInstance => instance != null || FindFirstObjectByType<TutorialManager>() != null;

    public static TutorialManager Instance
    {
        get
        {
            if (instance == null) instance = FindFirstObjectByType<TutorialManager>();
            if (instance == null)
            {
                GameObject go = new GameObject("TutorialManager");
                instance = go.AddComponent<TutorialManager>();
            }
            return instance;
        }
    }

    enum TutorialStep
    {
        NotStarted,
        WaitForPC,
        WaitForOrderAccepted,
        WaitForPartPickup,
        WaitForPartPlaced,
        WaitForAssembly,
        WaitForDelivery,
        Complete
    }

    TutorialStep currentStep = TutorialStep.NotStarted;
    bool tutorialActive;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        RuntimeGameBootstrap.EnsureCoreSystems();
    }

    void Start()
    {
        bool completed = SaveSystem.LoadBool(TutorialCompleteKey, false);
        if (!completed)
        {
            tutorialActive = true;
            StartTutorial();
        }
    }

    void StartTutorial()
    {
        OnOrderAccepted += HandleOrderAccepted;
        OnPartPickedUp  += HandlePartPickedUp;
        OnPartPlacedOnBench += HandlePartPlaced;
        OnGunAssembled  += HandleGunAssembled;
        OnGunDelivered  += HandleGunDelivered;

        AdvanceTo(TutorialStep.WaitForPC);
    }

    void AdvanceTo(TutorialStep step)
    {
        if (!tutorialActive) return;
        currentStep = step;

        switch (step)
        {
            case TutorialStep.WaitForPC:
                ShowHint("Walk to the computer and press E to check your first order.");
                GameFeedback.Show("TUTORIAL: Approach the computer and press E.", 6f);
                break;

            case TutorialStep.WaitForOrderAccepted:
                ShowHint("Review the order then click Accept to take the job.");
                GameFeedback.Show("TUTORIAL: Accept the order on the computer.", 6f);
                break;

            case TutorialStep.WaitForPartPickup:
                ShowHint("Find the required parts on the shelves. Look at a part and press E to pick it up.");
                GameFeedback.Show("TUTORIAL: Pick up a part from the shelves.", 6f);
                break;

            case TutorialStep.WaitForPartPlaced:
                ShowHint("Bring the part to the Assembly Bench. Aim at the correct slot and press E to place it.");
                GameFeedback.Show("TUTORIAL: Place the part in the matching bench slot.", 6f);
                break;

            case TutorialStep.WaitForAssembly:
                ShowHint("Fill all required slots, then press E on the red Assemble button.");
                GameFeedback.Show("TUTORIAL: Fill all bench slots, then hit the Assemble button.", 6f);
                break;

            case TutorialStep.WaitForDelivery:
                ShowHint("Put the gun in a Gun Case, then carry the case to the Conveyor Belt and press E.");
                GameFeedback.Show("TUTORIAL: Case the gun and ship it on the conveyor.", 6f);
                break;

            case TutorialStep.Complete:
                CompleteTutorial();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Event handlers
    // -------------------------------------------------------------------------

    void HandleOrderAccepted()
    {
        if (currentStep == TutorialStep.WaitForPC || currentStep == TutorialStep.WaitForOrderAccepted)
            AdvanceTo(TutorialStep.WaitForPartPickup);
    }

    void HandlePartPickedUp()
    {
        if (currentStep == TutorialStep.WaitForPartPickup)
            AdvanceTo(TutorialStep.WaitForPartPlaced);
    }

    void HandlePartPlaced()
    {
        if (currentStep == TutorialStep.WaitForPartPlaced)
            AdvanceTo(TutorialStep.WaitForAssembly);
    }

    void HandleGunAssembled()
    {
        if (currentStep == TutorialStep.WaitForAssembly)
            AdvanceTo(TutorialStep.WaitForDelivery);
    }

    void HandleGunDelivered()
    {
        if (currentStep == TutorialStep.WaitForDelivery)
            AdvanceTo(TutorialStep.Complete);
    }

    // -------------------------------------------------------------------------
    // Triggered by OrderAccepted — nudge tutorial forward if player opens PC
    // -------------------------------------------------------------------------
    public void NotifyPCOpened()
    {
        if (currentStep == TutorialStep.WaitForPC)
            AdvanceTo(TutorialStep.WaitForOrderAccepted);
    }

    // -------------------------------------------------------------------------
    // Completion
    // -------------------------------------------------------------------------

    void CompleteTutorial()
    {
        tutorialActive = false;
        UnsubscribeAll();
        SaveSystem.SaveBool(TutorialCompleteKey, true);

        ShowHint(string.Empty);
        SummaryOverlayManager.Instance.ShowSummary(
            "Tutorial Complete",
            "You know the loop. Watch your heat and keep the money flowing.",
            5f);
        GameFeedback.Show("Tutorial complete. You're on your own now.", 4f);
    }

    void UnsubscribeAll()
    {
        OnOrderAccepted  -= HandleOrderAccepted;
        OnPartPickedUp   -= HandlePartPickedUp;
        OnPartPlacedOnBench -= HandlePartPlaced;
        OnGunAssembled   -= HandleGunAssembled;
        OnGunDelivered   -= HandleGunDelivered;
    }

    void ShowHint(string message)
    {
        if (GameplayUIManager.HasLiveInstance)
            GameplayUIManager.Instance.SetContextHint(string.IsNullOrWhiteSpace(message) ? string.Empty : $"[Tutorial] {message}");
    }

    // -------------------------------------------------------------------------
    // Public fire helpers (called from other scripts)
    // -------------------------------------------------------------------------
    public static void FireOrderAccepted()     => OnOrderAccepted?.Invoke();
    public static void FirePartPickedUp()      => OnPartPickedUp?.Invoke();
    public static void FirePartPlaced()        => OnPartPlacedOnBench?.Invoke();
    public static void FireGunAssembled()      => OnGunAssembled?.Invoke();
    public static void FireGunDelivered()      => OnGunDelivered?.Invoke();

    void OnDestroy()
    {
        if (tutorialActive) UnsubscribeAll();
    }
}
