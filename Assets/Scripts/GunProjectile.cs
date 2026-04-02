using System;
using System.Collections.Generic;
using UnityEngine;

public class GunProjectile : MonoBehaviour
{
    float speed;
    float maxDistance;
    float radius;
    float damage;
    float impactForce;
    float distanceTravelled;
    Vector3 direction = Vector3.forward;
    readonly List<Transform> ignoredRoots = new List<Transform>();
    TrailRenderer trailRenderer;

    public static GunProjectile Create(
        Vector3 position,
        Vector3 direction,
        float speed,
        float maxDistance,
        float radius,
        float damage,
        float impactForce,
        Color color,
        params Transform[] ignoreRoots)
    {
        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "GunProjectile";
        projectileObject.transform.position = position;
        projectileObject.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        projectileObject.transform.localScale = Vector3.one * Mathf.Max(0.03f, radius * 2f);

        Collider collider = projectileObject.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        GunProjectile projectile = projectileObject.AddComponent<GunProjectile>();
        projectile.Initialize(direction, speed, maxDistance, radius, damage, impactForce, color, ignoreRoots);
        return projectile;
    }

    void Update()
    {
        float stepDistance = speed * Time.deltaTime;
        if (stepDistance <= 0f) return;

        Ray ray = new Ray(transform.position, direction);
        RaycastHit[] hits = Physics.SphereCastAll(ray, radius, stepDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (ShouldIgnoreCollider(hitCollider)) continue;

            Impact(hits[i]);
            return;
        }

        transform.position += direction * stepDistance;
        distanceTravelled += stepDistance;
        if (distanceTravelled >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    void Initialize(
        Vector3 shotDirection,
        float projectileSpeed,
        float projectileRange,
        float projectileRadius,
        float projectileDamage,
        float projectileImpactForce,
        Color color,
        Transform[] ignoreRoots)
    {
        direction = shotDirection.sqrMagnitude > 0.001f ? shotDirection.normalized : Vector3.forward;
        speed = Mathf.Max(1f, projectileSpeed);
        maxDistance = Mathf.Max(1f, projectileRange);
        radius = Mathf.Clamp(projectileRadius, 0.015f, 0.18f);
        damage = Mathf.Max(1f, projectileDamage);
        impactForce = Mathf.Max(0f, projectileImpactForce);
        distanceTravelled = 0f;

        ignoredRoots.Clear();
        if (ignoreRoots != null)
        {
            for (int i = 0; i < ignoreRoots.Length; i++)
            {
                if (ignoreRoots[i] != null) ignoredRoots.Add(ignoreRoots[i]);
            }
        }

        ConfigureVisuals(color);
        Destroy(gameObject, Mathf.Max(1f, maxDistance / speed) + 0.25f);
    }

    void ConfigureVisuals(Color color)
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = CreateMaterial(color);
        }

        trailRenderer = gameObject.AddComponent<TrailRenderer>();
        trailRenderer.time = 0.08f;
        trailRenderer.minVertexDistance = 0.02f;
        trailRenderer.startWidth = radius * 1.5f;
        trailRenderer.endWidth = radius * 0.25f;
        trailRenderer.alignment = LineAlignment.View;
        trailRenderer.material = CreateMaterial(color);
        trailRenderer.startColor = new Color(color.r, color.g, color.b, 0.95f);
        trailRenderer.endColor = new Color(color.r, color.g, color.b, 0f);
    }

    Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = shader != null ? new Material(shader) : null;
        if (material != null) material.color = color;
        return material;
    }

    bool ShouldIgnoreCollider(Collider collider)
    {
        if (collider == null || !collider.enabled) return true;
        if (collider.transform == transform || collider.transform.IsChildOf(transform)) return true;

        for (int i = 0; i < ignoredRoots.Count; i++)
        {
            Transform root = ignoredRoots[i];
            if (root != null && collider.transform.IsChildOf(root)) return true;
        }

        return false;
    }

    void Impact(RaycastHit hit)
    {
        transform.position = hit.point;

        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.ApplyDamage(damage, hit.point, direction);
        }

        Rigidbody hitBody = hit.collider.attachedRigidbody;
        if (hitBody != null && !hitBody.isKinematic)
        {
            hitBody.AddForceAtPosition(direction * impactForce, hit.point, ForceMode.Impulse);
        }

        Destroy(gameObject);
    }
}
