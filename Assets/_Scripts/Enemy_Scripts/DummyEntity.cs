using UnityEngine;
using System.Collections;
using _Scripts.Interface;

public class DummyEntity : MonoBehaviour, IDamageable
{
    [Header("Data Entity (ScriptableObject)")]
    [SerializeField] private BaseEntitySO _entityData;

    [Header("Juicy Effect: Hit Flash (URP)")]
    [Tooltip("Material yang menggunakan Shader Custom/URP2D_WhiteFlash")]
    [SerializeField] private Material _flashMaterial;
    [SerializeField] private float _flashDuration = 0.08f;

    private float _currentHealth;
    private SpriteRenderer _spriteRenderer;
    private Material _originalMaterial;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null)
            _originalMaterial = _spriteRenderer.sharedMaterial;

        if (_entityData != null)
            _currentHealth = _entityData.MaxHealth;
    }

    private void OnDisable()
    {
        RestoreOriginalMaterial();
    }

    public void TakeDamage(float damageAmount)
    {
        if (_entityData != null && _entityData.IsImmortal)
        {
            TriggerHitFlash();
            return;
        }

        _currentHealth -= damageAmount;
       TriggerHitFlash();
    
        if (_currentHealth <= 0)
            Die();
    }

    private void TriggerHitFlash()
    {
        if (_spriteRenderer == null || _flashMaterial == null) return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        _spriteRenderer.sharedMaterial = _flashMaterial;

        yield return new WaitForSecondsRealtime(_flashDuration);

        RestoreOriginalMaterial();
        _flashRoutine = null;
    }

    private void RestoreOriginalMaterial()
    {
        if (_spriteRenderer != null && _originalMaterial != null)
            _spriteRenderer.sharedMaterial = _originalMaterial;
    }

    private void Die()
    {
        Debug.Log($"<color=red>[DEAD]</color> {_entityData.EntityName} hancur!");
        Destroy(gameObject);
    }
}