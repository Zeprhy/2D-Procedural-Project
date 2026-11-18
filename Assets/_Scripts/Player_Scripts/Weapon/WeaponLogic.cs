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

    [Header("Juicy Effect: Combat VFX")]
    [Tooltip("Komponen pengontrol efek visual benturan tebasan")]
    [SerializeField] private CombatVFXController _vfxController;

    [Header("Juicy Effect: Hit Stop / Freeze Frame")]
    [Tooltip("Nilai timeScale saat freeze (0.05f = micro slow-mo dramatis, 0.0f = pure freeze)")]
    [SerializeField] private float _hitStopTimeScale = 0.05f;
    [SerializeField] private float _normalHitStopDuration = 0.035f;
    [SerializeField] private float _criticalHitStopDuration = 0.09f;

    [Header("Weapon Trajectory Tracking")]
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

    [Header("Juicy Effect: Knockback & Recoil")]
    [SerializeField] private float _baseKnockbackForce = 7f;
    [SerializeField] private float _knockbackUpwardLift = 2.5f;
    [SerializeField] private float _playerRecoilForce = 3.5f;

    [Header("Juicy Effect: Floating Damage Text")]
    [SerializeField] private DamagePopUp2D _damageTextPrefab;

    private PolygonCollider2D _hitbox;
    private GameObject _currentOwner;
    private bool _isAttacking;
    private HashSet<IDamageable> _coolingDownTargets = new HashSet<IDamageable>();

    // Hit Stop State
    private float _hitStopEndTime;
    private Coroutine _hitStopRoutine;

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

    private void Awake()
    {
        _hitbox = GetComponent<PolygonCollider2D>();
        _hitbox.enabled = false;

        if (_vfxController == null)
            _vfxController = GetComponent<CombatVFXController>();

        if (_weaponTip == null)
            Debug.LogWarning($"<color=yellow>[WeaponLogic]</color> '_weaponTip' belum dipasang pada '{gameObject.name}'. Sistem akan menggunakan estimasi fallback dari Collider.", this);
    }

    private void Update()
    {
        // Trajectory hanya dicatat selama jendela serang (attack window) aktif
        if (_isAttacking)
        {
            RecordTrajectorySample();
        }
    }

    private void OnDisable()
    {
        RestoreTimeScale();
        _trajectoryBuffer.Clear();
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

        // Reset riwayat trajektori: abaikan gerakan mouse sebelum tombol serangan ditekan
        _trajectoryBuffer.Clear();
        RecordTrajectorySample();

        yield return new WaitForSeconds(data.AttackCooldown);

        _isAttacking = false;
        _hitbox.enabled = false;
        _trajectoryBuffer.Clear();
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

        // Pencegahan evaluasi berulang pada target yang sama
        if (_coolingDownTargets.Contains(damageable)) return;

        //  Hitung Titik Kontak Bilah Pedang Presisi (Segment Projection)
        Vector3 contactPoint = CalculateAccurateContactPoint(target);

        //  Evaluasi Syarat Trajektori & Head Hit untuk Critical
        bool isCritical = EvaluateCriticalHit(target, contactPoint);

        //  Kalkulasi Damage
        float baseDamage = (_weaponData is MeleeWeaponSO melee) ? melee.Damage : 10f;
        float finalDamage = isCritical ? (baseDamage * _criticalMultiplier) : baseDamage;

        //  Hitung Arah Benturan
        Vector2 hitDirection = Vector2.right;
        if (_currentOwner != null)
        {
            Vector2 toTarget = (target.bounds.center - _currentOwner.transform.position);
            if (toTarget.sqrMagnitude > 0.001f)
                hitDirection = toTarget.normalized;
        }

        //  Memicu Modular Combat VFX (Phase VFX)
        if (_vfxController != null)
        {
            if (isCritical)
                _vfxController.PlayCriticalHit(contactPoint, hitDirection);
            else
                _vfxController.PlayNormalHit(contactPoint, hitDirection);
        }

        //  Camera Shake & Zoom Punch
        float weaponWeight = _weaponData != null ? _weaponData.Weight : 5f;
        if (CameraFollow2D.Instance != null)
            CameraFollow2D.Instance.TriggerHitImpact(weaponWeight, isCritical);

        //  Floating Damage Text
        if (_damageTextPrefab != null)
        {
            Vector3 spawnPos = contactPoint + new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, 0);
            DamagePopUp2D popUp = Instantiate(_damageTextPrefab, spawnPos, Quaternion.identity);
            popUp.Setup(finalDamage, isCritical);
        }

        RequestHitStop(isCritical);

        float hitDirX = Mathf.Sign(hitDirection.x);
        float weightMod = Mathf.Max(0.5f, weaponWeight / 5f);
        float finalKnockback = _baseKnockbackForce * weightMod * (isCritical ? 1.4f : 1.0f);
        Vector2 knockbackVector = new Vector2(hitDirX * finalKnockback, _knockbackUpwardLift);

        if (_currentOwner != null)
        {
            PlatformerMovement playerMovement = _currentOwner.GetComponent<PlatformerMovement>();
            if (playerMovement != null)
                playerMovement.ApplyMicroRecoil(_playerRecoilForce, hitDirX);
        }

        // 10. Kunci Target dalam Cooldown & Terapkan Damage
        StartCoroutine(TargetDamageCooldownRoutine(damageable));
        damageable.TakeDamage(finalDamage, knockbackVector);
    }

    #region Trajectory & Critical Evaluation

    private void RecordTrajectorySample()
    {
        Vector3 tipPos = GetWeaponTipPosition();
        float time = Time.unscaledTime;

        _trajectoryBuffer.Enqueue(new TipSample(tipPos, time));

        // Buang data sampel yang melebihi jumlah batas atau terlalu lama
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

        // Proyeksikan pusat collider musuh ke segmen bilah pedang [hilt, tip]
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
        // Butuh minimal 2 sampel untuk menghitung vektor pergerakan
        if (_trajectoryBuffer.Count < 2) return false;

        TipSample[] samples = _trajectoryBuffer.ToArray();
        TipSample oldestSample = samples[0];
        TipSample newestSample = samples[samples.Length - 1];

        float deltaT = newestSample.Time - oldestSample.Time;
        if (deltaT <= 0.0001f) return false;

        // 1. Vektor Kecepatan Rata-Rata Ujung Senjata
        Vector2 avgVelocity = (Vector2)(newestSample.Position - oldestSample.Position) / deltaT;
        float currentSpeed = avgVelocity.magnitude;

        // Syarat A: Kecepatan cukup tinggi
        if (currentSpeed < _minSwingSpeedThreshold) return false;

        // Syarat B: Kecepatan vertikal ke bawah cukup kuat (Vy negatif)
        if (avgVelocity.y > -_minDownwardSpeed) return false;

        // Syarat C: Sudut tebasan mengarah ke bawah (deviasi dari Vector2.down)
        float angleDeviation = Vector2.Angle(avgVelocity.normalized, Vector2.down);
        if (angleDeviation > _maxDownwardAngleDeviation) return false;

        // Syarat D: Titik kontak mengenai area kepala musuh (Head Zone)
        float targetHeight = target.bounds.size.y;
        float headThresholdY = target.bounds.max.y - (targetHeight * _headZoneRatio);
        bool isHeadHit = contactPoint.y >= headThresholdY;

        return isHeadHit;
    }

    #endregion

    #region Hit Stop Management

    private void RequestHitStop(bool isCritical)
    {
        float duration = isCritical ? _criticalHitStopDuration : _normalHitStopDuration;
        if (duration <= 0f) return;

        float newEndTime = Time.unscaledTime + duration;
        if (newEndTime > _hitStopEndTime)
            _hitStopEndTime = newEndTime;

        if (_hitStopRoutine == null)
            _hitStopRoutine = StartCoroutine(HitStopWorker());
    }

    private IEnumerator HitStopWorker()
    {
        Time.timeScale = _hitStopTimeScale;

        while (Time.unscaledTime < _hitStopEndTime)
        {
            yield return null;
        }

        RestoreTimeScale();
    }

    private void RestoreTimeScale()
    {
        Time.timeScale = 1.0f;
        _hitStopEndTime = 0f;
        if (_hitStopRoutine != null)
        {
            StopCoroutine(_hitStopRoutine);
            _hitStopRoutine = null;
        }
    }

    #endregion
}