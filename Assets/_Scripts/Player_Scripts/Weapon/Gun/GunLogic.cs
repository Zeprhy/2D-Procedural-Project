using UnityEngine;

public class GunLogic : WeaponLogic
{
    [Header("Transform Referensi Senjata Api")]
    [Header("Transform Referensi Senjata Api")]
    [Tooltip("Titik keluarnya proyektil (Muzzle Point)")]
    [SerializeField] private Transform _firePoint;

    [Header("Layer Penyaringan Tabrakan Peluru")]
    [Tooltip("Layer musuh yang menerima damage (IDamageable)")]
    [SerializeField] private LayerMask _targetLayer;
    [Tooltip("Layer rintangan lingkungan (Ground & Wall)")]
    [SerializeField] private LayerMask _obstacleLayer;

    [Header("Sistem Energi Bersama (Stage 6.2)")]
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
    }

    private void Update()
    {
        if (_fireCooldownTimer > 0f)
            _fireCooldownTimer -= Time.deltaTime;
    }

     /// <summary>
    /// Diinisialisasi oleh WeaponEquipmentManager saat dipasang
    /// </summary>
    public void InitializeGun(GunEnergyCore core, bool isRightHand)
    {
        _energyCore = core;
        IsRightHand = isRightHand;
        _isTriggerHeld = false;
        _fireCooldownTimer = 0f;

        if (_aimTracker == null)
            _aimTracker = FindFirstObjectByType<PlayerAimTracker2D>();
    }

    /// <summary>
    /// Dipanggil oleh WeaponEquipmentManager saat senjata dipasang ke tangan
    /// </summary>
    public void SetEnergyCore(GunEnergyCore core) => _energyCore = core;
    public void SetHandOrientation(bool isRightHand) => IsRightHand = isRightHand;
    public override void TriggerAttack(GameObject owner)
    {
        if (_weaponData != null && !IsAttacking)
            _currentOwner = owner;
    }

    #region Independent Trigger Lifecycle

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

        // Tidak ada tembakan tombol yang di tahan
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
        if (_fireCooldownTimer > 0) return;

        GunWeaponSO gunData = _weaponData as GunWeaponSO;
        if (gunData == null)
        {
            Debug.LogWarning($"<color=yellow>[GunLogic]</color> Gagal menembak: WeaponData bukan GunWeaponSO!", this);
            return;
        }

        if (gunData.ProjectilePrefab == null)
        {
            Debug.LogWarning($"<color=yellow>[GunLogic]</color> Gagal menembak: ProjectilePrefab belum di-assign pada '{gunData.WeaponName}'!", this);
            return;
        }

        if (FirePoint == null)
        {
            Debug.LogWarning($"<color=yellow>[GunLogic]</color> Gagal menembak: FirePoint null pada '{gameObject.name}'!", this);
            return;
        }

        if (_energyCore == null)
        {
            Debug.LogWarning($"<color=yellow>[GunLogic]</color> Gagal menembak: GunEnergyCore belum terhubung!", this);
            return;
        }

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
            projectile.Initialize(
                fireDirection,
                gunData.DamagePerShot,
                gunData.ProjectileSpeed,
                gunData.ProjectileLifetime,
                _targetLayer,
                _obstacleLayer,
                owner
            );
        }
        else
        {
            Debug.LogError($"<color=red>[GunLogic]</color> ProjectilePrefab '{gunData.ProjectilePrefab.name}' tidak memiliki komponen EnergyProjectile2D!", this);
            Destroy(bulletObj);
        }
    }

    public virtual void PerformShoot(GameObject owner, GunWeaponSO data)
    {
        Debug.Log($"<color=orange>[GunLogic]</color> '{data.WeaponName}' ditembakkan oleh {owner.name}!", this);
    }
}