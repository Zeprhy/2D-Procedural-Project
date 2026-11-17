using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using _Scripts.Interface;

[RequireComponent(typeof(PolygonCollider2D))]
public class WeaponLogic : MonoBehaviour
{
    [Header("Data Senjata")]
    [SerializeField] private BaseWeaponSO _weaponData;

    [Header("Pengaturan Damage & Cooldown")]
    [SerializeField] private float _damageInterval = 0.5f;

    [Header("Target Layer")]
    [SerializeField] private LayerMask _targetLayer;

    [Header("Juicy Effect: Hit Stop & Critical")]
    [SerializeField] private float _criticalSwingThreshold = 250f;
    [SerializeField] private float _hitStopDuration = 0.08f;
    [SerializeField] private float _criticalMultiplier = 2.0f;

    [Header("Juicy Effect: Knockback & Recoil")]
    [SerializeField] private float _baseKnockbackForce = 7f;
    [SerializeField] private float _knockbackUpwardLift = 2.5f;
    [SerializeField] private float _playerRecoilForce = 3.5f;

    [Header("Juicy Effect: Floating Damage Text")]
    [SerializeField] private DamagePopUp2D _damageTextPrefab;

    private float _lastWeaponAngle;
    private float _currentAngularSpeed;
    private PolygonCollider2D _hitbox;
    private GameObject _currentOwner;
    private bool _isAttacking;
    private HashSet<IDamageable> _coolingDownTargets = new HashSet<IDamageable>();

    private void Awake()
    {
        _hitbox = GetComponent<PolygonCollider2D>();
        _hitbox.enabled = false;
    }

    private void Update()
    {
        float currentAngle = transform.eulerAngles.z;
        float angleDelta = Mathf.DeltaAngle(_lastWeaponAngle, currentAngle);

        _currentAngularSpeed = angleDelta / Time.deltaTime;
        _lastWeaponAngle = currentAngle;
    }

    public void TriggerAttack(GameObject owner)
    {
        if (_weaponData != null && !_isAttacking)
        {
            _currentOwner = owner;
            _weaponData.Activate(owner, this);
        }
    }

    public void PerformMeleeSlash(GameObject owner, MeleeWeaponSO data) => StartCoroutine(SlashRoutine(data));

    private IEnumerator SlashRoutine(MeleeWeaponSO data)
    {
        _isAttacking = true;
        _hitbox.enabled = true;

        yield return new WaitForSeconds(data.AttackCooldown);

        _isAttacking = false;
        _hitbox.enabled = false;
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0.05f; 
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
    }
    private IEnumerator TargetDamageCooldownRoutine(IDamageable victim)
    {
        _coolingDownTargets.Add(victim);
        
        yield return new WaitForSeconds(_damageInterval);
        
        _coolingDownTargets.Remove(victim);
    }

    private void OnTriggerEnter2D(Collider2D target) => ProcessDamage(target);
    private void OnTriggerStay2D(Collider2D target) => ProcessDamage(target);

    private void ProcessDamage(Collider2D target)
    {
        if (target.gameObject == _currentOwner) return;
        
        // Pengecekan Layer
        if (((1 << target.gameObject.layer) & _targetLayer.value) == 0) return;

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        if (_coolingDownTargets.Contains(damageable)) return;

        // Hitung Critical
        bool isCritical = Mathf.Abs(_currentAngularSpeed) > _criticalSwingThreshold;
        float baseDamage = (_weaponData is MeleeWeaponSO melee) ? melee.Damage : 10f;
        float finalDamage = isCritical ? (baseDamage * _criticalMultiplier) : baseDamage;

        // Camera Shake (Eksisting)
        float weaponWeight = _weaponData != null ? _weaponData.Weight : 5f;
        if (CameraFollow2D.Instance != null)
            CameraFollow2D.Instance.TriggerHitImpact(weaponWeight, isCritical);

        // Floating Damage Pop up (Eksisting)
        if (_damageTextPrefab != null)
        {
            Vector3 spawnPos = target.bounds.center + new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, 0);
            DamagePopUp2D popUp = Instantiate(_damageTextPrefab, spawnPos, Quaternion.identity);
            popUp.Setup(finalDamage, isCritical);
        }

        // Hit stop (Eksisting)
        if (isCritical)
            StartCoroutine(HitStopRoutine(_hitStopDuration));

        // Knockback + Recoil fisik
        float hitDirX = 1f;
        if (_currentOwner != null)
        {
            float diffX = target.bounds.center.x - _currentOwner.transform.position.x;
            if (Mathf.Abs(diffX) > 0.01f)
                hitDirX = Mathf.Sign(diffX);
            else
                hitDirX = Mathf.Sign(transform.lossyScale.x);
        }

        // Modulasi knockback berdasarkan Weight senjata dan critical
        float weightMod = Mathf.Max(0.5f, weaponWeight / 5f);
        float finalKnockback = _baseKnockbackForce * weightMod * (isCritical ? 1.4f : 1.0f);
        Vector2 knockbackVector = new Vector2(hitDirX * finalKnockback, _knockbackUpwardLift);

        if (_currentOwner != null)
        {
            PlatformerMovement playermovement = _currentOwner.GetComponent<PlatformerMovement>();
            if (playermovement != null)
                playermovement.ApplyMicroRecoil(_playerRecoilForce, hitDirX);
        }

        // TargetDamage Interval
        StartCoroutine(TargetDamageCooldownRoutine(damageable));
        if (damageable != null)
            damageable.TakeDamage(finalDamage);
    }
}