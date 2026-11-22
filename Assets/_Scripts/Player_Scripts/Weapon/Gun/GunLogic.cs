using UnityEngine;

public class GunLogic : WeaponLogic
{
    [Header("Transform Referensi Senjata Api")]
    [Tooltip("Titik keluarnya proyektil (Muzzle Point)")]
    [SerializeField] private Transform _firePoint;

    [Header("Layer Penyaringan Tabrakan Peluru")]
    [Tooltip("Layer musuh yang menerima damage (IDamageable)")]
    [SerializeField] private LayerMask _targetLayer;
    [Tooltip("Layer rintangan lingkungan (Ground & Wall)")]
    [SerializeField] private LayerMask _obstacleLayer;

    [Header("Sistem Energi Bersama")]
    [SerializeField] protected GunEnergyCore _energyCore;

    public Transform FirePoint => _firePoint != null ? _firePoint : transform;
    public GunEnergyCore EnergyCore => _energyCore;
    public bool IsRightHand { get; private set; } = true;

    private float _fireCooldownTimer;
    private bool _isTriggerHeld;
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
    }

    private void Update()
    {
        if (_fireCooldownTimer > 0f)
        {
            _fireCooldownTimer -= Time.deltaTime;
        }
    }

    public void InitializeGun(GunEnergyCore core, bool isRightHand)
    {
        _energyCore = core;
        IsRightHand = isRightHand;
        _isTriggerHeld = false;
        _fireCooldownTimer = 0f;

        if (_aimTracker == null)
            _aimTracker = FindFirstObjectByType<PlayerAimTracker2D>();
    }

    public void SetEnergyCore(GunEnergyCore core) => _energyCore = core;
    public void SetHandOrientation(bool isRightHand) => IsRightHand = isRightHand;

    public override void TriggerAttack(GameObject owner)
    {
        _currentOwner = owner;
    }

    #region Independent Trigger Lifecycle (Release-to-Fire)

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
        // Pistol tidak menembak saat ditahan (No automatic fire)
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
        if (gunData == null) return;
        if (gunData.ProjectilePrefab == null) return;
        if (FirePoint == null) return;
        if (_energyCore == null) return;
        if (!_energyCore.TryConsumeShoot()) return;
        SpawnEnergyProjectile(gunData, owner);
        _fireCooldownTimer = gunData.FireRate;
    }

    private void SpawnEnergyProjectile(GunWeaponSO gunData, GameObject owner)
    {
        if (_aimTracker == null)
            _aimTracker = FindFirstObjectByType<PlayerAimTracker2D>();

        Vector3 mouseWorldPos = _aimTracker != null ? _aimTracker.MouseWorldPosition : (FirePoint.position + FirePoint.right);
        Vector2 fireDirection = ((Vector2)mouseWorldPos - (Vector2)FirePoint.position);

        if (fireDirection.sqrMagnitude < 0.0001f)
            fireDirection = FirePoint.right;

        fireDirection.Normalize();

        GameObject bulletObj = Instantiate(gunData.ProjectilePrefab, FirePoint.position, Quaternion.identity);
        EnergyProjectile2D projectile = bulletObj.GetComponent<EnergyProjectile2D>();

        if (projectile != null)
        {
            // Operkan _vfxController dan _damageTextPrefab ke dalam proyektil!
            projectile.Initialize(
                fireDirection,
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
    }
}