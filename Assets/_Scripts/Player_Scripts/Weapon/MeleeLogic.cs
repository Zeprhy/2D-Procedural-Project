using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using _Scripts.Interface;

[RequireComponent(typeof(PolygonCollider2D))]
public class MeleeLogic : WeaponLogic
{
    [Header("Pengaturan Damage & Cooldown Melee")]
    [SerializeField] private float _damageInterval = 0.5f;
    [SerializeField] private LayerMask _targetLayer;

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
    private HashSet<IDamageable> _coolingDownTargets = new HashSet<IDamageable>();

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
        _hitbox.enabled = false;

        if (_weaponTip == null)
        {
            Debug.LogWarning($"<color=yellow>[MeleeLogic]</color> '_weaponTip' belum dipasang pada '{gameObject.name}'. Sistem akan menggunakan estimasi fallback dari Collider.", this);
        }
    }

    private void Update()
    {
        // Trajectory hanya dicatat selama jendela serang (attack window) aktif
        if (IsAttacking)
        {
            RecordTrajectorySample();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _trajectoryBuffer.Clear();
    }

    public override void TriggerAttack(GameObject owner)
    {
        if (_weaponData != null && !IsAttacking)
        {
            _currentOwner = owner;
            _weaponData.Activate(owner, this);
        }
    }

    public void PerformMeleeSlash(GameObject owner, MeleeWeaponSO data) => StartCoroutine(SlashRoutine(data));

    private IEnumerator SlashRoutine(MeleeWeaponSO data)
    {
        IsAttacking = true;
        _hitbox.enabled = true;

        _trajectoryBuffer.Clear();
        RecordTrajectorySample();

        yield return new WaitForSeconds(data.AttackCooldown);

        IsAttacking = false;
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

        if (((1 << target.gameObject.layer) & _targetLayer.value) == 0) return;

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        if (_coolingDownTargets.Contains(damageable)) return;

        // 1. Titik Kontak Presisi
        Vector3 contactPoint = CalculateAccurateContactPoint(target);

        // 2. Evaluasi Syarat Trajektori & Head Hit untuk Critical
        bool isCritical = EvaluateCriticalHit(target, contactPoint);

        // 3. Kalkulasi Damage
        float baseDamage = (_weaponData is MeleeWeaponSO melee) ? melee.Damage : 10f;
        float finalDamage = isCritical ? (baseDamage * _criticalMultiplier) : baseDamage;

        // 4. Hitung Arah Benturan
        Vector2 hitDirection = Vector2.right;
        if (_currentOwner != null)
        {
            Vector2 toTarget = (target.bounds.center - _currentOwner.transform.position);
            if (toTarget.sqrMagnitude > 0.001f)
                hitDirection = toTarget.normalized;
        }

        // 5. Memicu Modular Combat VFX
        if (_vfxController != null)
        {
            if (isCritical)
                _vfxController.PlayCriticalHit(contactPoint, hitDirection);
            else
                _vfxController.PlayNormalHit(contactPoint, hitDirection);
        }

        // 6. Camera Shake & Zoom Punch
        float weaponWeight = _weaponData != null ? _weaponData.Weight : 5f;
        if (CameraFollow2D.Instance != null)
            CameraFollow2D.Instance.TriggerHitImpact(weaponWeight, isCritical);

        // 7. Floating Damage Text
        if (_damageTextPrefab != null)
        {
            Vector3 spawnPos = contactPoint + new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, 0);
            DamagePopUp2D popUp = Instantiate(_damageTextPrefab, spawnPos, Quaternion.identity);
            popUp.Setup(finalDamage, isCritical);
        }

        // 8. Hit Stop
        RequestHitStop(isCritical);

        // 9. Knockback Musuh & Recoil Player
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

        // 10. Terapkan Damage
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