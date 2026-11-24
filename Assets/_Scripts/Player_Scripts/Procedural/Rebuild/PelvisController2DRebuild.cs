using UnityEngine;

[DisallowMultipleComponent]
public class PelvisController2DRebuild : MonoBehaviour
{
    [Header("Referensi Karakter & Input")]
    [SerializeField] private PlatformerMovement _movement;
    [SerializeField] private ProceduralBipedLeg2DRebuild _legController;
    [SerializeField] private Transform _facingReference;

    [Header("Walk Bobbing (Lokal)")]
    [Tooltip("Intensitas naik-turun pinggul saat berjalan")]
    [SerializeField] private float _walkBobAmount = 0.055f;
    [SerializeField] private float _smoothFollowSpeed = 14f;

    [Header("Idle Breathing (Nafas Halus)")]
    [Tooltip("Intensitas nafas halus saat karakter diam")]
    [SerializeField] private float _idleBreathingAmount = 0.02f;
    [Tooltip("Frekuensi nafas per detik")]
    [SerializeField] private float _breathingFrequency = 2.4f;

    [Header("Landing Impact Dip")]
    [Tooltip("Penurunan posisi lokal pinggul sesaat saat menyerap benturan pendaratan")]
    [SerializeField] private float _maxLandingDip = 0.075f;
    [Tooltip("Kecepatan pemulihan dari landing dip (detik)")]
    [SerializeField] private float _landingDipRecoverySpeed = 0.20f;

    [Header("Landing Detection Stability (P0)")]
    [Tooltip("Durasi grounded harus stabil sebelum dianggap benar-benar mendarat")]
    [SerializeField] private float _groundStableTime = 0.06f;
    [Tooltip("Durasi minimum di udara sebelum bisa dianggap mendarat")]
    [SerializeField] private float _minAirTimeForLanding = 0.10f;
    [Tooltip("Jeda antar landing dip untuk mencegah retrigger akibat flicker grounded")]
    [SerializeField] private float _landingCooldown = 0.25f;
    [Tooltip("Kecepatan jatuh minimum saat menyentuh tanah agar dianggap landing nyata")]
    [SerializeField] private float _landingMinFallSpeed = 2.0f;

    [Header("Inertia Filter (P0)")]
    [Tooltip("Waktu smoothing akselerasi (detik) untuk meredam noise velocity")]
    [SerializeField] private float _accelSmoothing = 0.08f;
    [Tooltip("Deadzone akselerasi sebelum inertia tilt berlaku")]
    [SerializeField] private float _accelDeadzone = 3f;

    [Header("Forward Walking Lean (Kemiringan Tubuh)")]
    [Tooltip("Sudut kecondongan tubuh ke depan saat berjalan (derajat)")]
    [SerializeField] private float _forwardWalkLeanAngle = 4.5f;

    [Header("Body Tilt & Inersia Pengereman")]
    [Tooltip("Sudut kemiringan maksimum saat akselerasi/rem (derajat)")]
    [SerializeField] private float _maxInertiaTilt = 6f;
    [Tooltip("Sensitivitas terhadap inersia horizontal")]
    [SerializeField] private float _tiltSensitivity = 0.7f;
    [SerializeField] private float _tiltRecoverySpeed = 10f;

    // Baseline Local Space (100% Bebas dari Bug Melayang)
    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;

    private float _currentLandingDip = 0f;
    private float _currentTotalTilt = 0f;
    private float _lastVelocityX = 0f;
    private float _accelEma = 0f;

    // Landing detection stability (P0)
    private float _groundedStableTimer = 0f;
    private float _airTime = 0f;
    private float _landingCooldownTimer = 0f;
    private float _lastAirborneVelocityY = 0f;

    private void Awake()
    {
        _initialLocalPosition = transform.localPosition;
        _initialLocalRotation = transform.localRotation;

        Transform root = transform.root;

        if (_movement == null && root != null)
            _movement = root.GetComponent<PlatformerMovement>();

        if (_legController == null && root != null)
            _legController = root.GetComponent<ProceduralBipedLeg2DRebuild>();

        if (_facingReference == null && root != null)
            _facingReference = root;
    }

    private void LateUpdate()
    {
        UpdateLandingImpactDetection();
        UpdatePelvisPosition();
        UpdatePelvisRotation();
    }

    #region Safe Movement Queries

    private Vector2 GetVelocity()
    {
        if (_movement != null && _movement.PlayerRigidbody != null)
            return _movement.PlayerRigidbody.linearVelocity;

        return Vector2.zero;
    }

    private bool GetIsGrounded()
    {
        return _movement != null && _movement.IsGrounded;
    }

    #endregion

    private void UpdateLandingImpactDetection()
    {
        float dt = Time.deltaTime;
        if (_landingCooldownTimer > 0f)
            _landingCooldownTimer -= dt;

        bool isGroundedNow = GetIsGrounded();
        Vector2 vel = GetVelocity();

        if (isGroundedNow)
        {
            _groundedStableTimer += dt;
        }
        else
        {
            _groundedStableTimer = 0f;
            _airTime += dt;
            _lastAirborneVelocityY = vel.y;
            return;
        }

        // P0: landing hanya jika airtime cukup, grounded stabil, jatuh cukup cepat, dan tanpa cooldown.
        bool landed = _airTime >= _minAirTimeForLanding
                      && _groundedStableTimer >= _groundStableTime
                      && _lastAirborneVelocityY <= -_landingMinFallSpeed
                      && _landingCooldownTimer <= 0f;

        if (landed)
        {
            _currentLandingDip = _maxLandingDip;
            _landingCooldownTimer = _landingCooldown;
            _airTime = 0f;
        }
        else if (_groundedStableTimer >= _groundStableTime)
        {
            // Tidak memenuhi syarat landing (mis. flicker grounded di tangga) -> buang airtime.
            _airTime = 0f;
        }
    }

    private void UpdatePelvisPosition()
    {
        float verticalOffset = 0f;
        Vector2 vel = GetVelocity();

        if (GetIsGrounded())
        {
            float vx = Mathf.Abs(vel.x);

            if (vx > 0.12f || (_movement != null && _movement.IsMoving))
            {
                // WALK BOBBING: Sinkron dengan ayunan langkah kaki
                float cycle = _legController != null ? _legController.WalkCycleNormalized : (Time.time * 6f);
                verticalOffset = -Mathf.Abs(Mathf.Sin(cycle * Mathf.PI * 2f)) * _walkBobAmount;
            }
            else
            {
                // IDLE BREATHING
                verticalOffset = Mathf.Sin(Time.time * _breathingFrequency) * _idleBreathingAmount;
            }
        }

        // Pemulihan Landing Dip secara elastis
        if (_currentLandingDip > 0f)
        {
            float recoveryRate = (_maxLandingDip / Mathf.Max(0.05f, _landingDipRecoverySpeed)) * Time.deltaTime;
            _currentLandingDip = Mathf.MoveTowards(_currentLandingDip, 0f, recoveryRate);
        }

        // Terapkan posisi lokal murni
        float targetLocalY = _initialLocalPosition.y + verticalOffset - _currentLandingDip;
        Vector3 targetPos = new Vector3(_initialLocalPosition.x, targetLocalY, _initialLocalPosition.z);

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * _smoothFollowSpeed);
    }

    private void UpdatePelvisRotation()
    {
        Vector2 vel = GetVelocity();
        float currentVx = vel.x;
        float facingDir = _facingReference != null ? Mathf.Sign(_facingReference.lossyScale.x) : 1f;

        // 1. FORWARD WALKING LEAN (Arah Benar Hadap Kanan & Kiri)
        float forwardLeanAngle = 0f;
        if (GetIsGrounded() && Mathf.Abs(currentVx) > 0.15f)
        {
            float maxSpeed = (_movement != null) ? Mathf.Max(1f, _movement.Speed) : 6f;
            float speedRatio = Mathf.Clamp01(Mathf.Abs(currentVx) / maxSpeed);

            // Tentukan apakah bergerak maju atau mundur relatif terhadap arah hadap
            float moveDirX = Mathf.Sign(currentVx);
            float forwardMultiplier = (moveDirX == facingDir) ? 1f : -0.7f; // Condong ke belakang jika berjalan mundur

            forwardLeanAngle = speedRatio * _forwardWalkLeanAngle * forwardMultiplier;
        }

        // 2. ACCELERATION / BRAKING INERTIA TILT (P0: EMA-filtered, deadzone)
        float dt = Time.deltaTime;
        float rawAccel = (currentVx - _lastVelocityX) / Mathf.Max(0.0001f, dt);
        _lastVelocityX = currentVx;

        float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, _accelSmoothing));
        _accelEma = Mathf.Lerp(_accelEma, rawAccel, alpha);

        float inertiaTilt = 0f;
        if (Mathf.Abs(_accelEma) > _accelDeadzone)
        {
            float localAccel = _accelEma * facingDir;
            inertiaTilt = Mathf.Clamp((localAccel / 12f) * _tiltSensitivity * _maxInertiaTilt, -_maxInertiaTilt, _maxInertiaTilt);
        }

        // 3. SINTESIS ROTASI TUNGGAL RELATIF TERHADAP BIND-POSE
        float targetTotalTilt = forwardLeanAngle + inertiaTilt;
        _currentTotalTilt = Mathf.Lerp(_currentTotalTilt, targetTotalTilt, Time.deltaTime * _tiltRecoverySpeed);

        // Rotasi lokal -Z memiringkan tubuh ke depan pada rig lokal
        transform.localRotation = _initialLocalRotation * Quaternion.Euler(0f, 0f, -_currentTotalTilt);
    }
}
