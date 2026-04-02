using UnityEngine;
using UnityEngine.InputSystem;

public class GunWeapon : MonoBehaviour
{
    [Header("Weapon")]
    public float damage = 34f;
    public float fireRate = 4.5f;
    public float range = 85f;
    public float impactForce = 18f;
    public float incorrectAssemblySpread = 0.05f;

    [Header("Projectile")]
    public float projectileSpeed = 90f;
    public float projectileRadius = 0.04f;
    public float projectileSpawnOffset = 0.22f;
    public Color projectileColor = new Color(1f, 0.78f, 0.2f, 1f);

    float nextFireTime;
    bool isCorrectAssembly = true;
    PlayerController playerController;

    public void Configure(PlayerController controller, bool correctAssembly, GunOrder order)
    {
        playerController = controller;
        isCorrectAssembly = correctAssembly;

        if (order == null) return;

        switch (order.riskLevel)
        {
            case OrderRisk.Medium:
                damage += 4f;
                fireRate += 0.25f;
                break;
            case OrderRisk.High:
                damage += 8f;
                fireRate += 0.5f;
                break;
        }
    }

    void Awake()
    {
        EnsurePartType();
    }

    void Update()
    {
        EnsurePartType();

        if (!IsHeldByPlayer()) return;
        if (PCManager.Instance != null && PCManager.Instance.IsInPC) return;
        if (Time.time < nextFireTime) return;
        if (!ReadAttackDown()) return;

        Fire();
    }

    bool IsHeldByPlayer()
    {
        return PlayerInventory.Instance != null && PlayerInventory.Instance.heldItem == gameObject;
    }

    bool ReadAttackDown()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame) return true;
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) return true;
        return Input.GetMouseButtonDown(0);
    }

    void Fire()
    {
        Camera viewCamera = ResolveViewCamera();
        if (viewCamera == null) return;

        nextFireTime = Time.time + (1f / Mathf.Max(0.1f, fireRate));

        Vector3 direction = viewCamera.transform.forward;
        if (!isCorrectAssembly)
        {
            direction = ApplySpread(direction, incorrectAssemblySpread);
        }

        Vector3 spawnPosition = viewCamera.transform.position + direction * projectileSpawnOffset;
        GunProjectile.Create(
            spawnPosition,
            direction,
            projectileSpeed,
            range,
            projectileRadius,
            damage,
            impactForce,
            projectileColor,
            playerController != null ? playerController.transform : null,
            transform);
    }

    Camera ResolveViewCamera()
    {
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null && playerController.ViewCamera != null) return playerController.ViewCamera;
        return Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
    }

    Vector3 ApplySpread(Vector3 direction, float amount)
    {
        Vector2 spread = UnityEngine.Random.insideUnitCircle * amount;
        Vector3 spreadDirection = direction +
                                  (ResolveViewCamera()?.transform.right ?? Vector3.right) * spread.x +
                                  (ResolveViewCamera()?.transform.up ?? Vector3.up) * spread.y;
        return spreadDirection.normalized;
    }

    void EnsurePartType()
    {
        Part part = GetComponent<Part>();
        if (part != null) part.type = PartType.Assembled;
    }
}
