using UnityEngine;

namespace _Scripts.Interface
{
    public interface IDamageable
    {
        void TakeDamage(float damageAmount);
        void TakeDamage(float damageAmount, Vector2 knockbackForce);
    }
}