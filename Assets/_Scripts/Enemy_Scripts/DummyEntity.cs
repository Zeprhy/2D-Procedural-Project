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

    [Header("Juicy Effect: Squash & Stretch (Damage Impact)")]
    [Tooltip("Pengali tinggi Y saat tertekan/terbentur tebasan")]
    [SerializeField] private float _squashYMultiplier = 0.72f;
    [Tooltip("Pengali tinggi Y saat membal memanjang")]
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
        _rb = GetComponent<Rigidbody2D>();
        _rb.freezeRotation = true;

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (_visualTransform == null && _spriteRenderer != null)
        {
            _visualTransform = _spriteRenderer.transform;
        }

        if (_spriteRenderer != null)
        {
            _originalMaterial = _spriteRenderer.sharedMaterial;
        }

        if (_visualTransform != null)
        {
            _originalScale = _visualTransform.localScale;
        }

        if (_entityData != null)
            _currentHealth = _entityData.MaxHealth;
    }

    private void OnDisable()
    {
        RestoreOriginalMaterial();
        RestoreOriginalScale();
    }

    public void TakeDamage(float damageAmount) => TakeDamage(damageAmount, Vector2.zero);
    public void TakeDamage(float damageAmount, Vector2 knockbackForce)
    {
        ApplyKnockback(knockbackForce);
        TriggerHitFlash();
        TriggerSquashAndStretch();

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

    private void TriggerSquashAndStretch()
    {
        if (_visualTransform == null) return;

        if (_squashRoutine != null)
            StopCoroutine(_squashRoutine);

        _squashRoutine = StartCoroutine(SquashAndStretchRoutine());
    }

    private IEnumerator SquashAndStretchRoutine()
    {
        Vector3 initialScale = _visualTransform.localScale;
        
        Vector3 squashScale = new Vector3(_originalScale.x, _originalScale.y * _squashYMultiplier, _originalScale.z);
        Vector3 stretchScale = new Vector3(_originalScale.x, _originalScale.y * _stretchYMultiplier, _originalScale.z);

        // FASE 1: SQUASH
        float elapsed = 0f;
        while (elapsed < _squashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _squashDuration);
            _visualTransform.localScale = Vector3.Lerp(initialScale, squashScale, t);
            yield return null;
        }
        _visualTransform.localScale = squashScale;

        // FASE 2: STRETCH
        elapsed = 0f;
        while (elapsed < _stretchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _stretchDuration);
            _visualTransform.localScale = Vector3.Lerp(squashScale, stretchScale, t);
            yield return null;
        }
        _visualTransform.localScale = stretchScale;

        // FASE 3: RECOVER
        elapsed = 0f;
        while (elapsed < _recoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _recoverDuration));
            _visualTransform.localScale = Vector3.Lerp(stretchScale, _originalScale, t);
            yield return null;
        }

        RestoreOriginalScale();
        _squashRoutine = null;
    }

    /// <summary>
    /// Dipanggil dari WallCollisionReaction2D saat entitas menabrak dinding secara horizontal
    /// </summary>
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

        // Kompresi pada sumbu X (menabrak dinding tegak lurus), sedikit melebar di sumbu Y (preservasi volume)
        float stretchY = 1.0f + ((1.0f - squashXMultiplier) * 0.45f);
        Vector3 squashScale = new Vector3(_originalScale.x * squashXMultiplier, _originalScale.y * stretchY, _originalScale.z);

        // FASE 1: Kompresi terhadap dinding
        float elapsed = 0f;
        while (elapsed < squashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / squashDuration);
            _visualTransform.localScale = Vector3.Lerp(initialScale, squashScale, t);
            yield return null;
        }
        _visualTransform.localScale = squashScale;

        // FASE 2: Pemulihan kembali ke skala asli
        elapsed = 0f;
        while (elapsed < recoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / recoverDuration));
            _visualTransform.localScale = Vector3.Lerp(squashScale, _originalScale, t);
            yield return null;
        }

        RestoreOriginalScale();
        _squashRoutine = null;
    }

    private void RestoreOriginalScale()
    {
        if (_visualTransform != null && _originalScale != Vector3.zero)
        {
            _visualTransform.localScale = _originalScale;
        }
    }

    #endregion

    private void Die()
    {
        Debug.Log($"<color=red>[DEAD]</color> {_entityData.EntityName} hancur!");
        Destroy(gameObject);
    }
}