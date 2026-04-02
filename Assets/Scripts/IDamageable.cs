using UnityEngine;

public interface IDamageable
{
    void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitDirection);
}
