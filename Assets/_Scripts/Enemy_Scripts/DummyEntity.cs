using UnityEngine;
using System.Collections;
using _Scripts.Interface;

public class DummyEntity : MonoBehaviour, IDamageable
{
    [Header("Data Entity (ScriptableObject)")]
    [SerializeField] private BaseEntitySO _entityData;

    private float _currentHealth;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null) _originalColor = _spriteRenderer.color;

        if (_entityData != null)
            _currentHealth = _entityData.MaxHealth;
    }

    public void TakeDamage(float damageAmount)
    {
        if (_entityData != null && _entityData.IsImmortal)
        {
            // Debug.Log($"<color=yellow>[DUMMY]</color> {_entityData.EntityName} kebal! Menerima {damageAmount} damage tapi tidak berkurang.");
            StartCoroutine(FlashRedEffect());
            return;
        }

        _currentHealth -= damageAmount;
        // Debug.Log($"<color=orange>[DUMMY HIT]</color> Sisa Nyawa: {_currentHealth}/{_entityData.MaxHealth}");

        StartCoroutine(FlashRedEffect());
        if (_currentHealth <= 0)
            Die();
    }

    private IEnumerator FlashRedEffect()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            _spriteRenderer.color = _originalColor;
        }
    }

    private void Die()
    {
        Debug.Log($"<color=red>[DEAD]</color> {_entityData.EntityName} hancur!");
        Destroy(gameObject);
    }
}