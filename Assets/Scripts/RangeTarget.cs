using System.Collections;
using UnityEngine;

public class RangeTarget : MonoBehaviour, IDamageable, IInteractionPromptProvider
{
    [Header("Target")]
    public float knockbackAngle = 28f;
    public float resetDelay = 1.15f;
    public Color idleColor = new Color(0.74f, 0.16f, 0.12f, 1f);
    public Color hitColor = new Color(1f, 0.9f, 0.28f, 1f);

    Quaternion idleRotation;
    Renderer targetRenderer;
    Coroutine resetRoutine;

    void Awake()
    {
        idleRotation = transform.localRotation;
        targetRenderer = GetComponent<Renderer>();
        SetColor(idleColor);
    }

    public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (resetRoutine != null) StopCoroutine(resetRoutine);

        transform.localRotation = idleRotation * Quaternion.Euler(-knockbackAngle, 0f, 0f);
        SetColor(hitColor);
        resetRoutine = StartCoroutine(ResetAfterDelay());
    }

    public string GetInteractionPrompt()
    {
        return "Range target";
    }

    IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);
        transform.localRotation = idleRotation;
        SetColor(idleColor);
        resetRoutine = null;
    }

    void SetColor(Color color)
    {
        if (targetRenderer == null) return;
        targetRenderer.material.color = color;
    }
}
