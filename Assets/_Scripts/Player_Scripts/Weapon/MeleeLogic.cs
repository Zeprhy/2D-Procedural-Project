using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using _Scripts.Interface;

[RequireComponent(typeof(PolygonCollider2D))]
public class MeleeLogic : WeaponLogic
{
    [Header("Pengaturan Damage & Cooldown Melee")]
    [Tooltip("Jeda waktu minimum sebelum target yang sama dapat menerima damage kembali di dalam hitbox aktif")]
    [SerializeField] private float _damageInterval = 0.25f;
    [SerializeField] private LayerMask _targetLayer;

    [Header("Sword Swing Trail (Opsional)")]
    [Tooltip("Pengontrol Trail Renderer untuk efek ayunan pedang. Jika kosong, akan dicari otomatis pada GameObject ini atau child.")]
    [SerializeField] private MeleeTrailVFXController _trailVFX;

    [Header("Weapon Trajectory Tracking (Melee)")]
    [Tooltip("Transform penanda di ujung bilah senjata (Weapon Tip)")]
    [SerializeField] private Transform _weaponTip;
    [Tooltip("Jumlah frame sampel untuk menghitung rata-rata kecepatan ujung pedang")]
    [SerializeField] private int _maxTrajectorySamples = 5;
    [Tooltip("Jendela waktu maksimal riwayat sampel trajektori (detik)")]
    [SerializeField] private float _maxSampleTimeWindow = 0.08f;

    [Header("Critical Hit Conditions (Overhead & Headshot)")]
    [Tooltip("Kecepatan linear minimum ujung senjata untuk memicu Critical")]
    [SerializeField] private float _minSwingSpeedThreshold = 9.0f;
    [Tooltip("Kecepatan vertikal ke bawah minimum (Vy negatif)")]
    [SerializeField] private float _minDownwardSpeed = 4.0f;
    [Tooltip("Toleransi sudut deviasi terhadap arah bawah mutlak (Vector2.down) dalam derajat")]
    [SerializeField] private float _maxDownwardAngleDeviation = 55.0f;
    [Tooltip("Persentase area teratas collider musuh yang dianggap kepala (0.3 = 30% area atas)")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float _headZoneRatio = 0.30f;
    [SerializeField] private float _criticalMultiplier = 2.0f;

    [Header("Juicy Effect: Knockback & Recoil (Melee)")]
    [SerializeField] private float _baseKnockbackForce = 7f;
    [SerializeField] private float _knockbackUpwardLift = 2.5f;
    [SerializeField] private float _playerRecoilForce = 3.5f;

    private PolygonCollider2D _hitbox;
    private readonly HashSet<IDamageable> _coolingDownTargets = new HashSet<IDamageable>();

    // Trajectory Ring Buffer
    private struct TipSample
    {
        public Vector3 Position;
        public float Time;

        public TipSample(Vector3 position, float time)
        {
            Position = position;
            Time = time;
        }
    }
    private readonly Queue<TipSample> _trajectoryBuffer = new Queue<TipSample>();

    protected override void Awake()
    {
        base.Awake();

        _hitbox = GetComponent<PolygonCollider2D>();
        if (_hitbox == null)
            _hitbox = GetComponentInChildren<PolygonCollider2D>();

        if (_hitbox != null)
        {
            _hitbox.isTrigger = true;
            _hitbox.enabled = false;
        }
        else
        {
            Debug.LogError($"<color=red>[MeleeLogic]</color> PolygonCollider2D tidak ditemukan pada '{gameObject.name}'!", this);
        }

        if (_weaponTip == null)
        {
            Transform tip = transform.Find("Weapon_Tip");
            if (tip != null) _weaponTip = tip;
            else Debug.LogWarning($"<color=yellow>[MeleeLogic]</color> '_weaponTip' belum dipasang pada '{gameObject.name}'. Menggunakan fallback.", this);
        }

        if (_trailVFX == null)
            _trailVFX.GetComponentInChildren<MeleeTrailVFXController>(true);
    }

    private void Update()
    {
        if (IsAttacking)
        {
            RecordTrajectorySample();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        IsAttacking = false;
        if (_hitbox != null) _hitbox.enabled = false;
        _trajectoryBuffer.Clear();
        _coolingDownTargets.Clear();
    }

    public override void TriggerAttack(GameObject owner)
    {
        if (!IsAttacking)
        {
            _currentOwner = owner;

            if (_weaponData != null)
            {
                _weaponData.Activate(owner, this);
            }
            else
            {
                PerformMeleeSlash(owner, null);
            }
        }
    }

    public void PerformMeleeSlash(GameObject owner, MeleeWeaponSO data)
    {
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(SlashRoutine(data));
        }
    }

    private IEnumerator SlashRoutine(MeleeWeaponSO data)
    {
        IsAttacking = true;
        if (_hitbox != null) _hitbox.enabled = true;

        _trajectoryBuffer.Clear();
        RecordTrajectorySample();

        if (_trailVFX != null)
            _trailVFX.BeginSlash();

        // Hitbox tetap aktif sepanjang attack window tanpa terpengaruh penahanan tombol mouse
        float duration = (data != null && data.AttackCooldown > 0f) ? data.AttackCooldown : 0.4f;
        yield return new WaitForSeconds(duration);

        IsAttacking = false;
        if (_hitbox != null) _hitbox.enabled = false;
        if (_trailVFX != null) _trailVFX.EndSlash();

        _trajectoryBuffer.Clear();
        _coolingDownTargets.Clear();
    }

    /// <summary>
    /// Mengelola jeda damage per-target menggunakan Realtime agar kebal terhadap perlambatan Time.timeScale (Hit Stop)
    /// </summary>
    private IEnumerator TargetDamageCooldownRoutine(IDamageable victim)
    {
        _coolingDownTargets.Add(victim);

        // Menggunakan Realtime agar interval tidak membengkak 20x lipat saat Hit Stop aktif
        float interval = _damageInterval > 0f ? _damageInterval : 0.2f;
        yield return new WaitForSecondsRealtime(interval);

        _coolingDownTargets.Remove(victim);
    }

    private void OnTriggerEnter2D(Collider2D target) => ProcessDamage(target);
    private void OnTriggerStay2D(Collider2D target) => ProcessDamage(target);

    private void ProcessDamage(Collider2D target)
    {
        //Abaikan collider pemilik sendiri
        if (target.gameObject == _currentOwner) return;
        if (_currentOwner != null && target.transform.IsChildOf(_currentOwner.transform)) return;

        //Pengecekan Layer (Loloskan jika _targetLayer bernilai 0 / belum diatur)
        if (_targetLayer.value != 0 && (((1 << target.gameObject.layer) & _targetLayer.value) == 0))
            return;

        //Cari komponen IDamageable
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        //Cegah damage setiap frame: periksa apakah target masih dalam jeda _damageInterval
        if (_coolingDownTargets.Contains(damageable)) return;

        // Titik Kontak Presisi
        Vector3 contactPoint = CalculateAccurateContactPoint(target);

        // Evaluasi Syarat Trajektori & Head Hit untuk Critical
        bool isCritical = EvaluateCriticalHit(target, contactPoint);

        // Kalkulasi Damage
        float baseDamage = (_weaponData is MeleeWeaponSO melee) ? melee.Damage : 25f;
        float finalDamage = isCritical ? (baseDamage * _criticalMultiplier) : baseDamage;

        // Hitung Arah Benturan
        Vector2 hitDirection = Vector2.right;
        if (_currentOwner != null)
        {
            Vector2 toTarget = (target.bounds.center - _currentOwner.transform.position);
            if (toTarget.sqrMagnitude > 0.001f)
                hitDirection = toTarget.normalized;
        }

        //Memicu Modular Combat VFX
        if (_vfxController != null)
        {
            if (isCritical)
                _vfxController.PlayCriticalHit(contactPoint, hitDirection);
            else
                _vfxController.PlayNormalHit(contactPoint, hitDirection);
        }

        //Camera Shake & Zoom Punch
        float weaponWeight = _weaponData != null ? _weaponData.Weight : 6f;
        if (CameraFollow2D.Instance != null)
            CameraFollow2D.Instance.TriggerHitImpact(weaponWeight, isCritical);

        //Floating Damage Text
        if (_damageTextPrefab != null)
        {
            Vector3 spawnPos = contactPoint + new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, 0);
            DamagePopUp2D popUp = Instantiate(_damageTextPrefab, spawnPos, Quaternion.identity);
            popUp.Setup(finalDamage, isCritical);
        }

        //Hit Stop
        RequestHitStop(isCritical);

        //Knockback Musuh & Recoil Player
        float hitDirX = Mathf.Sign(hitDirection.x);
        float weightMod = Mathf.Max(0.5f, weaponWeight / 5f);
        float finalKnockback = _baseKnockbackForce * weightMod * (isCritical ? 1.4f : 1.0f);
        Vector2 knockbackVector = new Vector2(hitDirX * finalKnockback, _knockbackUpwardLift);

        if (_currentOwner != null)
        {
            PlatformerMovement playerMovement = _currentOwner.GetComponent<PlatformerMovement>();
            if (playerMovement == null)
                playerMovement = _currentOwner.GetComponentInParent<PlatformerMovement>();

            if (playerMovement != null)
                playerMovement.ApplyMicroRecoil(_playerRecoilForce, hitDirX);
        }

        //Kunci Target Dalam Jeda Realtime & Terapkan Damage
        StartCoroutine(TargetDamageCooldownRoutine(damageable));
        damageable.TakeDamage(finalDamage, knockbackVector);
    }

    #region Trajectory & Critical Evaluation

    private void RecordTrajectorySample()
    {
        Vector3 tipPos = GetWeaponTipPosition();
        float time = Time.unscaledTime;

        _trajectoryBuffer.Enqueue(new TipSample(tipPos, time));

        while (_trajectoryBuffer.Count > _maxTrajectorySamples ||
               (time - _trajectoryBuffer.Peek().Time > _maxSampleTimeWindow && _trajectoryBuffer.Count > 2))
        {
            _trajectoryBuffer.Dequeue();
        }
    }

    private Vector3 GetWeaponTipPosition()
    {
        if (_weaponTip != null)
            return _weaponTip.position;

        if (_hitbox != null)
            return transform.position + (transform.right * _hitbox.bounds.extents.magnitude * 1.5f);

        return transform.position + (transform.right * 1.5f);
    }

    private Vector3 CalculateAccurateContactPoint(Collider2D target)
    {
        Vector3 hiltPos = transform.position;
        Vector3 tipPos = GetWeaponTipPosition();
        Vector3 bladeVec = tipPos - hiltPos;
        float bladeLengthSqr = bladeVec.sqrMagnitude;

        Vector3 closestPointOnBlade = hiltPos;
        if (bladeLengthSqr > 0.0001f)
        {
            float t = Mathf.Clamp01(Vector3.Dot(target.bounds.center - hiltPos, bladeVec) / bladeLengthSqr);
            closestPointOnBlade = hiltPos + (bladeVec * t);
        }

        return target.bounds.ClosestPoint(closestPointOnBlade);
    }

    private bool EvaluateCriticalHit(Collider2D target, Vector3 contactPoint)
    {
        if (_trajectoryBuffer.Count < 2) return false;

        TipSample[] samples = _trajectoryBuffer.ToArray();
        TipSample oldestSample = samples[0];
        TipSample newestSample = samples[samples.Length - 1];

        float deltaT = newestSample.Time - oldestSample.Time;
        if (deltaT <= 0.0001f) return false;

        Vector2 avgVelocity = (Vector2)(newestSample.Position - oldestSample.Position) / deltaT;
        float currentSpeed = avgVelocity.magnitude;

        if (currentSpeed < _minSwingSpeedThreshold) return false;
        if (avgVelocity.y > -_minDownwardSpeed) return false;

        float angleDeviation = Vector2.Angle(avgVelocity.normalized, Vector2.down);
        if (angleDeviation > _maxDownwardAngleDeviation) return false;

        float targetHeight = target.bounds.size.y;
        float headThresholdY = target.bounds.max.y - (targetHeight * _headZoneRatio);
        bool isHeadHit = contactPoint.y >= headThresholdY;

        return isHeadHit;
    }

    #endregion
}