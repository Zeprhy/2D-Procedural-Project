using UnityEngine;
using System.Collections;
using _Scripts.Interface;

[RequireComponent(typeof(Rigidbody2D))]
public class DummyEntity : MonoBehaviour, IDamageable
{
    [Header("Data Entity (ScriptableObject)")]
    [SerializeField] private BaseEntitySO _entityData;

    [Header("Referensi Visual")]
    [Tooltip("Transform anak yang memegang SpriteRenderer. Jika kosong, akan mencari otomatis.")]
    [SerializeField] private Transform _visualTransform;

    [Header("Juicy Effect: Hit Flash (URP)")]
    [Tooltip("Material yang menggunakan Shader Custom/URP2D_WhiteFlash")]
    [SerializeField] private Material _flashMaterial;
    [SerializeField] private float _flashDuration = 0.08f;

    [Header("Juicy Effect: Squash & Stretch")]
    [Tooltip("Pengali tinggi Y saat tertekan/terbentur (0.7 = memendek 30%)")]
    [SerializeField] private float _squashYMultiplier = 0.72f;
    [Tooltip("Pengali tinggi Y saat membal memanjang (1.18 = memanjang 18%)")]
    [SerializeField] private float _stretchYMultiplier = 1.18f;
    [SerializeField] private float _squashDuration = 0.04f;
    [SerializeField] private float _stretchDuration = 0.07f;
    [SerializeField] private float _recoverDuration = 0.10f;

    [Header("Fisika Reaksi (Knockback)")]
    [SerializeField] private float _knockbackResistance = 0f;

    private float _currentHealth;
    private SpriteRenderer _spriteRenderer;
    private Material _originalMaterial;
    private Vector3 _originalScale;
    private Coroutine _flashRoutine;
    private Coroutine _squashRoutine;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();

        _rb.freezeRotation = true;

        if (_visualTransform == null && _spriteRenderer != null)
            _visualTransform = _spriteRenderer.transform;

        if (_spriteRenderer != null)
            _originalMaterial = _spriteRenderer.sharedMaterial;

        if (_visualTransform != null)
            _originalScale = _visualTransform.localScale;

        if (_entityData != null)
            _currentHealth = _entityData.MaxHealth;
    }

    private void OnDisable()
    {
        RestoreOriginalMaterial();
        RestoreOriginalScale();
    }

    // Overload 1: Tanpa knockback 
    public void TakeDamage(float damageAmount) => TakeDamage(damageAmount, Vector2.zero);
    
    // Overload 2: Dengan knockback fisik
    public void TakeDamage(float damageAmount, Vector2 knockbackForce)
    {
        ApplyKnockback(knockbackForce);
        TriggerHitFlash();
        TriggerSquashAndStrecth();

        if (_entityData != null && _entityData.IsImmortal)
            return;

        _currentHealth -= damageAmount;

        if (_currentHealth <= 0)
            Die();
    }

    private void ApplyKnockback(Vector2 force)
    {
        if (_rb == null || force == Vector2.zero) return;

        // Hitung resistansi (semakin tinggi, dorongan semakin kecil)
        float effectiveMultiplier = Mathf.Clamp01(1f - _knockbackResistance);
        Vector2 finalForce = force * effectiveMultiplier;

        // Reset velocity sesaat agar dorongan konsisten, lalu terapkan gaya
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        _rb.AddForce(finalForce, ForceMode2D.Impulse);
    }

    #region Hit Flash Logic
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
        {
            _spriteRenderer.sharedMaterial = _originalMaterial;
        }
    }

    #endregion

    #region Squash & Stretch Logic

    private void TriggerSquashAndStrecth()
    {
        if (_visualTransform == null) return;

        if (_squashRoutine != null)
            StopCoroutine(_squashRoutine);

        _squashRoutine = StartCoroutine(SquashAndStretchRoutine());
    }

    private IEnumerator SquashAndStretchRoutine()
    {
        Vector3 initialScale = _visualTransform.localScale;

        Vector3 SquashScale = new Vector3(_originalScale.x, _originalScale.y * _squashYMultiplier, _originalScale.z);
        Vector3 StretchScale = new Vector3(_originalScale.x, _originalScale.y * _stretchYMultiplier, _originalScale.z);

        float elapsed = 0f;
        while (elapsed < _squashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _squashDuration);
            _visualTransform.localScale = Vector3.Lerp(initialScale, SquashScale, t);
            yield return null;
        }

        _visualTransform.localScale = SquashScale;

        elapsed = 0f;
        while (elapsed < _stretchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _stretchDuration);
            _visualTransform.localScale = Vector3.Lerp(SquashScale, StretchScale, t);
            yield return null;
        }

        _visualTransform.localScale = StretchScale;

        elapsed = 0f;
        while (elapsed < _recoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _recoverDuration));
            _visualTransform.localScale = Vector3.Lerp(StretchScale, _originalScale, t);
            yield return null;
        }
        
        RestoreOriginalScale();
        _squashRoutine = null;
    }

    public void TriggerWallImpactSquash(float squashXMultiplier, float squashDuration, float recoverDuration)
    {
        if (_visualTransform == null) return;

        if (_squashRoutine != null)
            StopCoroutine(_squashRoutine);
        
        _squashRoutine = StartCoroutine(WallSquashRoutine(squashXMultiplier, squashDuration, recoverDuration));
    }

    private IEnumerator WallSquashRoutine(float squashXMultiplier, float squashDuration, float recoverDuration)
    {
        Vector3 initialScale = _visualTransform.localScale;

        float strechY = 1.0f + ((1.0f - squashXMultiplier) * 0.45f);
        Vector3 squashScale = new Vector3(_originalScale.x * squashXMultiplier, _originalScale.y * strechY, _originalScale.z);

        float elapsed = 0f;
        while (elapsed < squashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / squashDuration);
            _visualTransform.localScale = Vector3.Lerp(initialScale, squashScale, t);
            yield return null;
        }
        
        _visualTransform.localScale = squashScale;

        elapsed = 0f;
        while (elapsed < recoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / recoverDuration);
            _visualTransform.localScale = Vector3.Lerp(squashScale, _originalScale, t);
            yield return null;
        }

        RestoreOriginalScale();
        _squashRoutine = null;
    }

    private void RestoreOriginalScale()
    {
        if (_visualTransform != null && _originalScale != Vector3.zero)
            _visualTransform.localScale = _originalScale;
    }
    #endregion

    private void Die()
    {
        Debug.Log($"<color=red>[DEAD]</color> {_entityData.EntityName} hancur!");
        Destroy(gameObject);
    }
}