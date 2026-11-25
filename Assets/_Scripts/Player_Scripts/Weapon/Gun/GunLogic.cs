using UnityEngine;
using System.Collections;

public class GunLogic : WeaponLogic
{
    [Header("Transform Referensi Senjata Api")]
    [Tooltip("Titik keluarnya proyektil (Muzzle_Point/Fire_Point)")]
    [SerializeField] private Transform _firePoint;
    [Tooltip("Transform yang menerima recoil visual. Kosongkan untuk memakai root weapon (seluruh visual ikut bergerak).")]
    [SerializeField] private Transform _visualTransform;

    [Header("Layer Penyaringan Tabrakan Peluru")]
    [SerializeField] private LayerMask _targetLayer;
    [SerializeField] private LayerMask _obstacleLayer;

    [Header("Sistem Energi Bersama")]
    [SerializeField] protected GunEnergyCore _energyCore;

    [Header("Directional Energy Burst (Sprite Utama)")]
    [Tooltip("SpriteRenderer Energy Burst pada child Muzzle_Point/Energy_Burst")]
    [SerializeField] private SpriteRenderer _energyBurstRenderer;
    [Tooltip("Variasi sprite semburan energi tajam memanjang")]
    [SerializeField] private Sprite[] _energyBurstSprites;
    [Tooltip("Skala visual semburan energi")]
    [SerializeField] private Vector3 _energyBurstScale = Vector3.one;
    [Tooltip("Durasi tampil semburan energi sebelum menghilang otomatis")]
    [SerializeField] private float _energyBurstDuration = 0.045f;

    [Header("Energy Burst Particle Accent (Aksen Tambahan)")]
    [Tooltip("Particle System percikan kecil (2-4 sparks) di sekitar FirePoint")]
    [SerializeField] private ParticleSystem _energyBurstSparks;

    [Header("Overheat Steam VFX")]
    [Tooltip("Particle System uap panas yang keluar saat senjata Overheat")]
    [SerializeField] private ParticleSystem _overheatSteamParticles;

    public Transform FirePoint => _firePoint != null ? _firePoint : transform;
    public GunEnergyCore EnergyCore => _energyCore;
    public bool IsRightHand { get; private set; } = true;

    public float CurrentAimRecoil { get; private set; }
    private Vector3 _currentVisualRecoilPos;
    private float _currentVisualRecoilRot;
    private Vector3 _visualInitialLocalPos = Vector3.zero;
    private Quaternion _visualInitialLocalRot = Quaternion.identity;
    private Vector3 _lastAppliedLocalPos = Vector3.zero;
    private Quaternion _lastAppliedLocalRot = Quaternion.identity;
    private bool _hasVisualRestPose;
    private float _pendingArmRecoil;

    private float _fireCooldownTimer;
    private bool _isTriggerHeld;
    private Coroutine _energyBurstRoutine;
    private PlayerAimTracker2D _aimTracker;

    private static Transform _sharedVfxAnchor;

    private static Transform EnsureVfxAnchor()
    {
        if (_sharedVfxAnchor == null)
        {
            GameObject existing = GameObject.Find("MuzzleVFX_Anchor");
            if (existing != null)
            {
                _sharedVfxAnchor = existing.transform;
            }
            else
            {
                GameObject anchorObj = new GameObject("MuzzleVFX_Anchor");
                _sharedVfxAnchor = anchorObj.transform;
            }
        }
        return _sharedVfxAnchor;
    }

    protected override void Awake()
    {
        base.Awake();
        _aimTracker = FindFirstObjectByType<PlayerAimTracker2D>();

        if (_firePoint == null)
        {
            Transform preciseFirePoint = transform.Find("Muzzle_Point/Fire_Point");
            if (preciseFirePoint != null)
            {
                _firePoint = preciseFirePoint;
            }
            else
            {
                Transform muzzle = transform.Find("Muzzle_Point");
                if (muzzle != null) _firePoint = muzzle;
            }
        }

        // Recoil diterapkan ke root agar Muzzle_Point, Fire_Point, dan Grip_Point ikut bergerak.
        // Rest pose di-capture saat recoil pertama dipakai, sehingga grip alignment sudah selesai.
        if (_visualTransform == null)
            _visualTransform = transform;

        if (_energyBurstRenderer == null)
        {
            Transform burstObj = transform.Find("Muzzle_Point/Energy_Burst");
            if (burstObj != null) _energyBurstRenderer = burstObj.GetComponent<SpriteRenderer>();
        }

        if (_energyBurstSparks == null)
        {
            Transform sparksObj = transform.Find("Muzzle_Point/Energy_Spark");
            if (sparksObj != null) _energyBurstSparks = sparksObj.GetComponent<ParticleSystem>();
            else if (_energyBurstRenderer != null) _energyBurstSparks = _energyBurstRenderer.GetComponent<ParticleSystem>();
        }

        Transform vfxAnchor = EnsureVfxAnchor();

        if (_energyBurstRenderer != null)
        {
            _energyBurstRenderer.transform.SetParent(vfxAnchor, true);
            _energyBurstRenderer.flipX = false;
            _energyBurstRenderer.enabled = false;
        }

        if (_energyBurstSparks != null)
        {
            _energyBurstSparks.transform.SetParent(vfxAnchor, true);
        }

        if (_overheatSteamParticles == null)
        {
            Transform steamObj = transform.Find("Muzzle_Point/Overheat_Steam");
            if (steamObj != null) _overheatSteamParticles = steamObj.GetComponent<ParticleSystem>();
        }

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

        if (_energyBurstRoutine != null)
        {
            StopCoroutine(_energyBurstRoutine);
            _energyBurstRoutine = null;
        }

        if (_energyBurstRenderer != null)
            _energyBurstRenderer.enabled = false;

        if (_energyBurstSparks != null && _energyBurstSparks.isPlaying)
            _energyBurstSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (_overheatSteamParticles != null)
            _overheatSteamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        CurrentAimRecoil = 0f;
        _pendingArmRecoil = 0f;
        ResetVisualRecoilState();
    }

    private void OnDestroy()
    {
        if (_energyBurstRenderer != null)
            Destroy(_energyBurstRenderer.gameObject);

        if (_energyBurstSparks != null)
            Destroy(_energyBurstSparks.gameObject);
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
        _pendingArmRecoil = 0f;
        ResetVisualRecoilState();

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

    /// <summary>
    /// Mengambil total impulse recoil lengan yang tertunda, lalu meresetnya.
    /// Dipanggil satu kali per frame oleh hand controller, bukan setiap kali Update.
    /// </summary>
    public float ConsumeArmRecoilImpulse()
    {
        float impulse = _pendingArmRecoil;
        _pendingArmRecoil = 0f;
        return impulse;
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

        TriggerDirectionalBurst(fireDir);

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
        EnsureVisualRestPose();

        // Snapshot kick (bukan akumulasi) agar tembakan berikutnya me-refresh recoil dari nilai penuh.
        _currentVisualRecoilPos = new Vector3(-gunData.WeaponRecoilDistance, 0f, 0f);
        _currentVisualRecoilRot = gunData.WeaponRecoilAngle;

        // Terapkan langsung di frame tembakan supaya muzzle flash memakai posisi visual yang benar.
        ApplyVisualRecoilPose();
    }

    private void ApplyAimRecoil(GunWeaponSO gunData)
    {
        CurrentAimRecoil = Mathf.Min(CurrentAimRecoil + gunData.AimRecoilKick, gunData.MaxAimRecoil);

        // Impulse arm: dikonsumsi satu kali per tembakan oleh hand controller.
        _pendingArmRecoil = Mathf.Min(_pendingArmRecoil + gunData.AimRecoilKick, gunData.MaxAimRecoil);
    }

    private void ApplyPlayerPush(GunWeaponSO gunData, GameObject owner, Vector2 fireDirection)
    {
        if (owner == null) return;

        PlatformerMovement movement = owner.GetComponent<PlatformerMovement>();
        if (movement == null) movement = owner.GetComponentInParent<PlatformerMovement>();

        if (movement != null)
        {
            float fireDirX = Mathf.Sign(fireDirection.x);
            movement.ApplyMicroRecoil(gunData.PlayerPushForce, fireDirX);
        }
    }

    private void UpdateRecoilRecovery()
    {
        GunWeaponSO gunData = _weaponData as GunWeaponSO;
        float weaponRecovery = gunData != null ? gunData.WeaponRecoilRecoverySpeed : 16f;
        float aimRecovery = gunData != null ? gunData.AimRecoilRecoverySpeed : 18f;

        EnsureVisualRestPose();

        // Frame-rate independent ease-out (menggantikan Lerp(dt * speed) yang bisa snap saat FPS rendah).
        float dt = Time.deltaTime;
        float visualT = weaponRecovery > 0f ? 1f - Mathf.Exp(-weaponRecovery * dt) : 1f;

        _currentVisualRecoilPos = Vector3.Lerp(_currentVisualRecoilPos, Vector3.zero, visualT);
        _currentVisualRecoilRot = Mathf.Lerp(_currentVisualRecoilRot, 0f, visualT);

        if (_currentVisualRecoilPos.sqrMagnitude < 0.0000001f && Mathf.Abs(_currentVisualRecoilRot) < 0.0001f)
        {
            _currentVisualRecoilPos = Vector3.zero;
            _currentVisualRecoilRot = 0f;
        }

        ApplyVisualRecoilPose();

        CurrentAimRecoil = Mathf.MoveTowards(CurrentAimRecoil, 0f, dt * aimRecovery);
    }

    private void EnsureVisualRestPose()
    {
        if (_visualTransform == null)
            _visualTransform = transform;

        if (!_hasVisualRestPose)
        {
            CaptureVisualRestPose();
            return;
        }

        // Pose ditulis pihak lain (mis. WeaponEquipmentManager.AlignWeaponGrip saat equip / pindah tangan):
        // jadikan pose tersebut rest pose baru dan batalkan recoil yang sedang berjalan.
        if (WasVisualPoseWrittenExternally())
        {
            _currentVisualRecoilPos = Vector3.zero;
            _currentVisualRecoilRot = 0f;
            CaptureVisualRestPose();
        }
    }

    private void CaptureVisualRestPose()
    {
        _visualInitialLocalPos = _visualTransform.localPosition;
        _visualInitialLocalRot = _visualTransform.localRotation;
        _lastAppliedLocalPos = _visualInitialLocalPos;
        _lastAppliedLocalRot = _visualInitialLocalRot;
        _hasVisualRestPose = true;
    }

    private bool WasVisualPoseWrittenExternally()
    {
        if (_visualTransform == null) return false;

        if ((_visualTransform.localPosition - _lastAppliedLocalPos).sqrMagnitude > 0.000001f)
            return true;

        return Quaternion.Angle(_visualTransform.localRotation, _lastAppliedLocalRot) > 0.01f;
    }

    private void ApplyVisualRecoilPose()
    {
        if (!_hasVisualRestPose || _visualTransform == null) return;

        // Rotasi lokal +Z selalu mengangkat moncong, termasuk saat GFX di-mirror (scale.x = -1),
        // karena mirror horizontal tidak membalik sumbu Y lokal.
        Quaternion recoilRotation = Quaternion.Euler(0f, 0f, _currentVisualRecoilRot);
        Vector3 kickback = _visualInitialLocalRot * _currentVisualRecoilPos;

        if (_visualTransform == transform)
        {
            // Orbit mengelilingi grip (origin holder) agar grip tidak lepas dari tangan saat moncong terangkat.
            _visualTransform.localPosition = recoilRotation * (_visualInitialLocalPos + kickback);
            _visualTransform.localRotation = recoilRotation * _visualInitialLocalRot;
        }
        else
        {
            _visualTransform.localPosition = _visualInitialLocalPos + kickback;
            _visualTransform.localRotation = _visualInitialLocalRot * recoilRotation;
        }

        // Simpan pose yang kita tulis, supaya perubahan dari luar bisa dibedakan saat recovery berikutnya.
        _lastAppliedLocalPos = _visualTransform.localPosition;
        _lastAppliedLocalRot = _visualTransform.localRotation;
    }

    private void ResetVisualRecoilState()
    {
        _currentVisualRecoilPos = Vector3.zero;
        _currentVisualRecoilRot = 0f;

        // Jangan menimpa pose yang baru saja di-align pihak lain (mis. pindah tangan).
        if (_hasVisualRestPose && _visualTransform != null && !WasVisualPoseWrittenExternally())
        {
            _visualTransform.localPosition = _visualInitialLocalPos;
            _visualTransform.localRotation = _visualInitialLocalRot;
        }

        // Capture ulang di Update berikutnya (aman bila grip di-align lagi saat object disabled).
        _hasVisualRestPose = false;
    }

    #endregion

    #region Directional Energy Burst & Particle Accent

    private void TriggerDirectionalBurst(Vector2 fireDirection)
    {
        if (_energyBurstRenderer != null)
        {
            _energyBurstRenderer.transform.position = FirePoint.position;

            if (_energyBurstSprites != null && _energyBurstSprites.Length > 0)
            {
                Sprite randomSprite = _energyBurstSprites[Random.Range(0, _energyBurstSprites.Length)];
                if (randomSprite != null)
                    _energyBurstRenderer.sprite = randomSprite;
            }

            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            _energyBurstRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _energyBurstRenderer.transform.localScale = _energyBurstScale;
            _energyBurstRenderer.flipX = false;

            if (_energyBurstRoutine != null)
                StopCoroutine(_energyBurstRoutine);

            _energyBurstRoutine = StartCoroutine(EnergyBurstRoutine());
        }

        if (_energyBurstSparks != null)
        {
            _energyBurstSparks.transform.position = FirePoint.position;
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            _energyBurstSparks.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _energyBurstSparks.Play();
        }
    }

    private IEnumerator EnergyBurstRoutine()
    {
        _energyBurstRenderer.enabled = true;
        yield return new WaitForSeconds(_energyBurstDuration);
        if (_energyBurstRenderer != null)
            _energyBurstRenderer.enabled = false;
        _energyBurstRoutine = null;
    }

    #endregion
}