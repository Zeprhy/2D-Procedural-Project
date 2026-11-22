using UnityEngine;
using System.Collections;

public class GunLogic : WeaponLogic
{
    [Header("Transform Referensi Senjata Api")]
    [Tooltip("Titik keluarnya proyektil (Muzzle Point)")]
    [SerializeField] private Transform _firePoint;
    [Tooltip("Transform visual sprite senjata untuk kickback visual (jika kosong, auto-detect child Weapon_Sprite)")]
    [SerializeField] private Transform _visualTransform;

    [Header("Layer Penyaringan Tabrakan Peluru")]
    [SerializeField] private LayerMask _targetLayer;
    [SerializeField] private LayerMask _obstacleLayer;

    [Header("Sistem Energi Bersama")]
    [SerializeField] protected GunEnergyCore _energyCore;

    [Header("Shooting VFX")]
    [Tooltip("SpriteRenderer Muzzle Flash yang akan berkedip singkat saat menembak")]
    [SerializeField] private SpriteRenderer _muzzleFlashRenderer;
    [SerializeField] private float _muzzleFlashDuration = 0.05f;
    [Tooltip("Particle System pelepasan energi singkat di moncong senjata")]
    [SerializeField] private ParticleSystem _energyBurstParticles;

    [Header("Overheat Steam VFX")]
    [Tooltip("Particle System uap panas yang keluar saat senjata Overheat")]
    [SerializeField] private ParticleSystem _overheatSteamParticles;

    public Transform FirePoint => _firePoint != null ? _firePoint : transform;
    public GunEnergyCore EnergyCore => _energyCore;
    public bool IsRightHand { get; private set; } = true;

    // Recoil State
    public float CurrentAimRecoil { get; private set; }
    private Vector3 _currentVisualRecoilPos;
    private float _currentVisualRecoilRot;
    private Vector3 _visualInitialLocalPos = Vector3.zero;
    private Quaternion _visualInitialLocalRot = Quaternion.identity;

    private float _fireCooldownTimer;
    private bool _isTriggerHeld;
    private Coroutine _muzzleFlashRoutine;
    private PlayerAimTracker2D _aimTracker;

    protected override void Awake()
    {
        base.Awake();
        _aimTracker = FindFirstObjectByType<PlayerAimTracker2D>();

        if (_firePoint == null)
        {
            Transform muzzle = transform.Find("Muzzle_Point");
            if (muzzle != null) _firePoint = muzzle;
        }

        if (_visualTransform == null)
        {
            Transform spriteChild = transform.Find("Weapon_Sprite");
            if (spriteChild != null) _visualTransform = spriteChild;
        }

        if (_visualTransform != null)
        {
            _visualInitialLocalPos = _visualTransform.localPosition;
            _visualInitialLocalRot = _visualTransform.localRotation;
        }

        if (_muzzleFlashRenderer != null)
            _muzzleFlashRenderer.enabled = false;

        if (_overheatSteamParticles != null && _overheatSteamParticles.isPlaying)
            _overheatSteamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (_fireCooldownTimer > 0f)
            _fireCooldownTimer -= Time.deltaTime;

        UpdateRecoilRecovery();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (_energyCore != null)
            _energyCore.OnStateChanged -= HandleEnergyStateChanged;

        if (_muzzleFlashRoutine != null)
        {
            StopCoroutine(_muzzleFlashRoutine);
            _muzzleFlashRoutine = null;
        }

        if (_muzzleFlashRenderer != null)
            _muzzleFlashRenderer.enabled = false;

        if (_overheatSteamParticles != null)
            _overheatSteamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        CurrentAimRecoil = 0f;
    }

    public void InitializeGun(GunEnergyCore core, bool isRightHand)
    {
        if (_energyCore != null)
            _energyCore.OnStateChanged -= HandleEnergyStateChanged;

        _energyCore = core;
        IsRightHand = isRightHand;
        _isTriggerHeld = false;
        _fireCooldownTimer = 0f;
        CurrentAimRecoil = 0f;

        if (_energyCore != null)
        {
            _energyCore.OnStateChanged += HandleEnergyStateChanged;
            HandleEnergyStateChanged(_energyCore.CurrentState);
        }

        if (_aimTracker == null)
            _aimTracker = FindFirstObjectByType<PlayerAimTracker2D>();
    }

    public void SetEnergyCore(GunEnergyCore core)
    {
        InitializeGun(core, IsRightHand);
    }

    public void SetHandOrientation(bool isRightHand)
    {
        IsRightHand = isRightHand;
    }

    private void HandleEnergyStateChanged(GunSystemState state)
    {
        if (state == GunSystemState.OVERHEAT)
        {
            if (_overheatSteamParticles != null && !_overheatSteamParticles.isPlaying)
                _overheatSteamParticles.Play();
        }
        else
        {
            if (_overheatSteamParticles != null && _overheatSteamParticles.isPlaying)
                _overheatSteamParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public override void TriggerAttack(GameObject owner)
    {
        _currentOwner = owner;
    }

    #region Trigger Lifecycle (Hold to Aim -> Release to Fire)

    public override void OnTriggerDown(GameObject owner)
    {
        _currentOwner = owner;
        _isTriggerHeld = true;

        if (_energyCore != null)
            _energyCore.NotifyTriggerState(IsRightHand, true);
    }

    public override void OnTriggerHeld(GameObject owner)
    {
        _currentOwner = owner;
        _isTriggerHeld = true;

        if (_energyCore != null)
            _energyCore.NotifyTriggerState(IsRightHand, true);
    }

    public override void OnTriggerUp(GameObject owner)
    {
        _currentOwner = owner;
        _isTriggerHeld = false;

        if (_energyCore != null)
            _energyCore.NotifyTriggerState(IsRightHand, false);

        ExecuteFireTransaction(owner);
    }

    #endregion

    private void ExecuteFireTransaction(GameObject owner)
    {
        if (_fireCooldownTimer > 0f) return;

        GunWeaponSO gunData = _weaponData as GunWeaponSO;
        if (gunData == null || gunData.ProjectilePrefab == null || FirePoint == null || _energyCore == null)
            return;

        if (!_energyCore.TryConsumeShot()) return;

        Vector2 fireDir = SpawnEnergyProjectile(gunData, owner);

        ApplyWeaponRecoil(gunData);
        ApplyAimRecoil(gunData);
        ApplyPlayerPush(gunData, owner, fireDir);
        TriggerMuzzleEffects();

        _fireCooldownTimer = gunData.FireRate;
    }

    private Vector2 SpawnEnergyProjectile(GunWeaponSO gunData, GameObject owner)
    {
        if (_aimTracker == null)
            _aimTracker = FindFirstObjectByType<PlayerAimTracker2D>();

        Vector3 mouseWorldPos = _aimTracker != null ? _aimTracker.MouseWorldPosition : (FirePoint.position + FirePoint.right);
        Vector2 baseDirection = ((Vector2)mouseWorldPos - (Vector2)FirePoint.position);

        if (baseDirection.sqrMagnitude < 0.0001f)
            baseDirection = FirePoint.right;

        baseDirection.Normalize();
        float facingDir = Mathf.Sign(transform.lossyScale.x);
        Vector2 finalFireDirection = Quaternion.Euler(0f, 0f, CurrentAimRecoil * facingDir) * baseDirection;

        GameObject bulletObj = Instantiate(gunData.ProjectilePrefab, FirePoint.position, Quaternion.identity);
        EnergyProjectile2D projectile = bulletObj.GetComponent<EnergyProjectile2D>();

        if (projectile != null)
        {
            projectile.Initialize(
                finalFireDirection,
                gunData.DamagePerShot,
                gunData.ProjectileSpeed,
                gunData.ProjectileLifetime,
                _targetLayer,
                _obstacleLayer,
                owner,
                _vfxController,
                _damageTextPrefab
            );
        }
        else
        {
            Destroy(bulletObj);
        }

        return finalFireDirection;
    }

    #region Recoil & Push Logic

    private void ApplyWeaponRecoil(GunWeaponSO gunData)
    {
        _currentVisualRecoilPos = new Vector3(-gunData.WeaponRecoilDistance, 0f, 0f);
        _currentVisualRecoilRot = gunData.WeaponRecoilAngle;
    }

    private void ApplyAimRecoil(GunWeaponSO gunData)
    {
        CurrentAimRecoil = Mathf.Min(CurrentAimRecoil + gunData.AimRecoilKick, gunData.MaxAimRecoil);
    }

    private void ApplyPlayerPush(GunWeaponSO gunData, GameObject owner, Vector2 fireDirection)
    {
        if (owner == null) return;

        PlatformerMovement movement = owner.GetComponent<PlatformerMovement>();
        if (movement == null) movement = owner.GetComponentInParent<PlatformerMovement>();

        if (movement != null)
        {
            // Dorong berlawanan dari arah horizontal tembakan
            float fireDirX = Mathf.Sign(fireDirection.x);
            movement.ApplyMicroRecoil(gunData.PlayerPushForce, fireDirX);
        }
    }

    private void UpdateRecoilRecovery()
    {
        GunWeaponSO gunData = _weaponData as GunWeaponSO;
        float weaponRecovery = gunData != null ? gunData.WeaponRecoilRecoverySpeed : 16f;
        float aimRecovery = gunData != null ? gunData.AimRecoilRecoverySpeed : 18f;

        // Pemulihan Visual Senjata
        _currentVisualRecoilPos = Vector3.Lerp(_currentVisualRecoilPos, Vector3.zero, Time.deltaTime * weaponRecovery);
        _currentVisualRecoilRot = Mathf.Lerp(_currentVisualRecoilRot, 0f, Time.deltaTime * weaponRecovery);

        if (_visualTransform != null)
        {
            _visualTransform.localPosition = _visualInitialLocalPos + _currentVisualRecoilPos;
            _visualTransform.localRotation = _visualInitialLocalRot * Quaternion.Euler(0f, 0f, _currentVisualRecoilRot);
        }

        CurrentAimRecoil = Mathf.MoveTowards(CurrentAimRecoil, 0f, Time.deltaTime * aimRecovery);
    }

    #endregion

    #region Shooting VFX Logic

    private void TriggerMuzzleEffects()
    {

        if (_energyBurstParticles != null)
        {
            _energyBurstParticles.Play();
        }

        if (_muzzleFlashRenderer != null)
        {
            if (_muzzleFlashRoutine != null)
                StopCoroutine(_muzzleFlashRoutine);

            _muzzleFlashRoutine = StartCoroutine(MuzzleFlashRoutine());
        }
    }

    private IEnumerator MuzzleFlashRoutine()
    {
        _muzzleFlashRenderer.enabled = true;
        yield return new WaitForSeconds(_muzzleFlashDuration);
        _muzzleFlashRenderer.enabled = false;
        _muzzleFlashRoutine = null;
    }

    #endregion
}