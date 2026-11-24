using UnityEngine;
using System.Collections;

public class GunLogic : WeaponLogic
{
    [Header("Transform Referensi Senjata Api")]
    [Tooltip("Titik keluarnya proyektil (Muzzle_Point/Fire_Point)")]
    [SerializeField] private Transform _firePoint;
    [Tooltip("Transform visual sprite senjata untuk kickback visual (jika kosong, auto-detect child Weapon_Sprite)")]
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

    // Recoil State
    public float CurrentAimRecoil { get; private set; }
    private Vector3 _currentVisualRecoilPos;
    private float _currentVisualRecoilRot;
    private Vector3 _visualInitialLocalPos = Vector3.zero;
    private Quaternion _visualInitialLocalRot = Quaternion.identity;

    private float _fireCooldownTimer;
    private bool _isTriggerHeld;
    private Coroutine _energyBurstRoutine;
    private PlayerAimTracker2D _aimTracker;

    protected override void Awake()
    {
        base.Awake();
        _aimTracker = FindFirstObjectByType<PlayerAimTracker2D>();

        // 1. Auto-find Fire Point yang presisi (Cari Fire_Point di dalam Muzzle_Point)
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

        // 2. Auto-find Visual Transform untuk kickback senjata
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

        // 3. Auto-find SpriteRenderer Energy Burst
        if (_energyBurstRenderer == null)
        {
            Transform burstObj = transform.Find("Muzzle_Point/Energy_Burst");
            if (burstObj != null) _energyBurstRenderer = burstObj.GetComponent<SpriteRenderer>();
        }

        if (_energyBurstRenderer != null)
            _energyBurstRenderer.enabled = false;

        // 4. Auto-find Particle System Sparks Accent
        if (_energyBurstSparks == null)
        {
            Transform sparksObj = transform.Find("Muzzle_Point/Energy_Sparks");
            if (sparksObj != null) _energyBurstSparks = sparksObj.GetComponent<ParticleSystem>();
            else if (_energyBurstRenderer != null) _energyBurstSparks = _energyBurstRenderer.GetComponent<ParticleSystem>();
        }

        // 5. Auto-find Overheat Steam ParticleSystem
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

        // Bersihkan state visual moncong seketika saat dinonaktifkan
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

        // 1. Validasi Baterai & Overheat Atomik
        if (!_energyCore.TryConsumeShot()) return;

        // 2. Spawn Proyektil Energi
        Vector2 fireDir = SpawnEnergyProjectile(gunData, owner);

        // 3. Picu Feedback Tembakan
        ApplyWeaponRecoil(gunData);
        ApplyAimRecoil(gunData);
        ApplyPlayerPush(gunData, owner, fireDir);

        // 4. Picu Directional Burst Sprite & Particle Accent
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
            float fireDirX = Mathf.Sign(fireDirection.x);
            movement.ApplyMicroRecoil(gunData.PlayerPushForce, fireDirX);
        }
    }

    private void UpdateRecoilRecovery()
    {
        GunWeaponSO gunData = _weaponData as GunWeaponSO;
        float weaponRecovery = gunData != null ? gunData.WeaponRecoilRecoverySpeed : 16f;
        float aimRecovery = gunData != null ? gunData.AimRecoilRecoverySpeed : 18f;

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

    #region Directional Energy Burst & Particle Accent

    private void TriggerDirectionalBurst(Vector2 fireDirection)
    {
        // 1. AKSI UTAMA: DIRECTIONAL BURST SPRITE
        if (_energyBurstRenderer != null)
        {
            // Pastikan posisi letupan terkunci presisi di titik FirePoint (tidak mengambang ke kursor)
            _energyBurstRenderer.transform.position = FirePoint.position;

            // Pilih variasi sprite acak jika tersedia
            if (_energyBurstSprites != null && _energyBurstSprites.Length > 0)
            {
                Sprite randomSprite = _energyBurstSprites[Random.Range(0, _energyBurstSprites.Length)];
                if (randomSprite != null)
                    _energyBurstRenderer.sprite = randomSprite;
            }

            // Orientasi semburan memanjang mengikuti sudut tembakan peluru
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            _energyBurstRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _energyBurstRenderer.transform.localScale = _energyBurstScale;

            // Hentikan coroutine sebelumnya jika tembakan terjadi sangat cepat (rapid fire safe)
            if (_energyBurstRoutine != null)
                StopCoroutine(_energyBurstRoutine);

            _energyBurstRoutine = StartCoroutine(EnergyBurstRoutine());
        }

        // 2. AKSEN TAMBAHAN: PARTICLE SPARKS
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