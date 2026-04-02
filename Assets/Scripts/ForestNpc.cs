using System.Collections;
using UnityEngine;

public class ForestNpc : MonoBehaviour, IDamageable, IInteractionPromptProvider
{
    [Header("NPC")]
    public float maxHealth = 60f;
    public float moveSpeed = 1.8f;
    public float aggroRange = 18f;
    public float stopDistance = 7f;
    public Color aliveColor = new Color(0.79f, 0.76f, 0.62f, 1f);
    public Color hurtColor = new Color(1f, 0.48f, 0.36f, 1f);
    public Color deadColor = new Color(0.28f, 0.08f, 0.08f, 1f);

    float currentHealth;
    bool isAlive = true;
    Renderer bodyRenderer;
    Transform playerTarget;
    Coroutine hurtFlashRoutine;

    public void Initialize(Transform player)
    {
        playerTarget = player;
        currentHealth = maxHealth;
        bodyRenderer = GetComponent<Renderer>();
        SetColor(aliveColor);
    }

    void Awake()
    {
        currentHealth = maxHealth;
        bodyRenderer = GetComponent<Renderer>();
        SetColor(aliveColor);
    }

    void Update()
    {
        if (!isAlive || playerTarget == null) return;

        Vector3 toPlayer = playerTarget.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        if (distance <= 0.01f) return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(toPlayer.normalized, Vector3.up),
            Time.deltaTime * 4f);

        if (distance > stopDistance && distance <= aggroRange)
        {
            transform.position += toPlayer.normalized * moveSpeed * Time.deltaTime;
        }
    }

    public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!isAlive) return;

        currentHealth -= Mathf.Max(1f, damage);
        if (currentHealth <= 0f)
        {
            Die(hitPoint, hitDirection);
            return;
        }

        if (hurtFlashRoutine != null) StopCoroutine(hurtFlashRoutine);
        hurtFlashRoutine = StartCoroutine(HurtFlash());
    }

    public string GetInteractionPrompt()
    {
        return isAlive ? "Forest scout" : "Scout down";
    }

    IEnumerator HurtFlash()
    {
        SetColor(hurtColor);
        yield return new WaitForSeconds(0.16f);
        if (isAlive) SetColor(aliveColor);
        hurtFlashRoutine = null;
    }

    void Die(Vector3 hitPoint, Vector3 hitDirection)
    {
        isAlive = false;
        if (hurtFlashRoutine != null)
        {
            StopCoroutine(hurtFlashRoutine);
            hurtFlashRoutine = null;
        }

        SetColor(deadColor);

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null) capsule.direction = 1;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 1.6f;
        rb.angularDamping = 0.15f;
        rb.linearDamping = 0.1f;
        rb.AddForceAtPosition(hitDirection.normalized * 16f, hitPoint, ForceMode.Impulse);
        rb.AddTorque(Random.onUnitSphere * 6f, ForceMode.Impulse);

        Destroy(gameObject, 8f);
    }

    void SetColor(Color color)
    {
        if (bodyRenderer == null) return;
        bodyRenderer.material.color = color;
    }
}
