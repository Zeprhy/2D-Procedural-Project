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

    [Header("Fisika Reaksi (Knockback)")]
    [SerializeField] private float _knockbackResistance = 0f; // 0 = Penuh, 1 = Kebal dorongan (Boss)

    private float _currentHealth;
    private SpriteRenderer _spriteRenderer;
    private Material _originalMaterial;
    private Coroutine _flashRoutine;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();

        _rb.freezeRotation = true;

        if (_spriteRenderer != null)
            _originalMaterial = _spriteRenderer.sharedMaterial;

        if (_entityData != null)
            _currentHealth = _entityData.MaxHealth;
    }

    private void OnDisable()
    {
        RestoreOriginalMaterial();
    }

    public void TakeDamage(float damageAmount) => TakeDamage(damageAmount, Vector2.zero);

    public void TakeDamage(float damageAmount, Vector2 knockbackForce)
    {
        ApplyKnockback(knockbackForce);
        TriggerHitFlash();

        if (_entityData != null && _entityData.IsImmortal)
            return;

        _currentHealth -= damageAmount;

        if (_currentHealth <= 0)
            Die();
    }

    private void ApplyKnockback(Vector2 force)
    {
        if (_rb == null || force == Vector2.zero) return;

        float effectiveMultiplier = Mathf.Clamp01(1f - _knockbackResistance);
        Vector2 finalForce = force * effectiveMultiplier;

        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        _rb.AddForce(finalForce, ForceMode2D.Impulse);
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