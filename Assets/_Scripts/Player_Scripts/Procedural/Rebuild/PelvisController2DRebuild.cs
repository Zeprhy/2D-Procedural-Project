using UnityEngine;

[DisallowMultipleComponent]
public class PelvisController2DRebuild : MonoBehaviour
{
    [Header("Referensi Karakter & Input")]
    [SerializeField] private PlatformerMovement _movement;
    [SerializeField] private ProceduralBipedLeg2DRebuild _legController;
    [SerializeField] private Transform _facingReference;

    [Header("Walk Bobbing")]
    [Tooltip("Intensitas naik-turun pinggul saat berjalan")]
    [SerializeField] private float _walkBobAmount = 0.055f;
    [Tooltip("Smooth time posisi pinggul (detik)")]
    [SerializeField] private float _positionSmoothTime = 0.07f;
    [Tooltip("Waktu blend bob saat mulai/berhenti berjalan (detik)")]
    [SerializeField] private float _bobBlendTime = 0.12f;

    [Header("Idle Breathing")]
    [Tooltip("Intensitas nafas halus saat karakter diam")]
    [SerializeField] private float _idleBreathingAmount = 0.02f;
    [Tooltip("Frekuensi nafas per detik")]
    [SerializeField] private float _breathingFrequency = 2.4f;

    [Header("Landing Impact Dip")]
    [Tooltip("Penurunan posisi lokal pinggul sesaat saat menyerap benturan pendaratan")]
    [SerializeField] private float _maxLandingDip = 0.075f;
    [Tooltip("Durasi pemulihan dari landing dip (detik)")]
    [SerializeField] private float _landingDipRecoverySpeed = 0.20f;

    [Header("Landing Detection Stability")]
    [Tooltip("Durasi grounded harus stabil sebelum dianggap benar-benar mendarat")]
    [SerializeField] private float _groundStableTime = 0.06f;
    [Tooltip("Durasi minimum di udara sebelum bisa dianggap mendarat")]
    [SerializeField] private float _minAirTimeForLanding = 0.10f;
    [Tooltip("Jeda antar landing dip untuk mencegah retrigger akibat flicker grounded")]
    [SerializeField] private float _landingCooldown = 0.25f;
    [Tooltip("Kecepatan jatuh minimum saat menyentuh tanah agar dianggap landing nyata")]
    [SerializeField] private float _landingMinFallSpeed = 2.0f;

    [Header("Inertia Filter")]
    [Tooltip("Waktu smoothing akselerasi (detik) untuk meredam noise velocity")]
    [SerializeField] private float _accelSmoothing = 0.08f;
    [Tooltip("Deadzone akselerasi sebelum inertia tilt berlaku")]
    [SerializeField] private float _accelDeadzone = 3f;

    [Header("Forward Walking Lean")]
    [Tooltip("Sudut kecondongan tubuh ke depan saat berjalan (derajat)")]
    [SerializeField] private float _forwardWalkLeanAngle = 4.5f;

    [Header("Slope Tilt (P2)")]
    [Tooltip("Seberapa besar pinggul mengikuti kemiringan tanah (0 = tidak ikut, 1 = penuh)")]
    [SerializeField][Range(0f, 1f)] private float _slopeTiltWeight = 0.5f;
    [Tooltip("Kecepatan blend kemiringan lereng")]
    [SerializeField] private float _slopeTiltSpeed = 8f;
    [Tooltip("Batas maksimum kemiringan lereng yang diteruskan ke pinggul (derajat)")]
    [SerializeField] private float _maxSlopeTiltAngle = 45f;

    [Header("Body Tilt & Inersia Pengereman")]
    [Tooltip("Sudut kemiringan maksimum saat akselerasi/rem (derajat)")]
    [SerializeField] private float _maxInertiaTilt = 6f;
    [Tooltip("Sensitivitas terhadap inersia horizontal")]
    [SerializeField] private float _tiltSensitivity = 0.7f;
    [SerializeField] private float _tiltRecoverySpeed = 10f;

    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private Vector3 _positionVelocity;

    private float _currentLandingDip;
    private float _currentTotalTilt;
    private float _lastVelocityX;
    private float _accelEma;

    private float _groundedStableTimer;
    private float _airTime;
    private float _landingCooldownTimer;
    private float _lastAirborneVelocityY;

    private float _bobWeight;
    private float _breathWeight;
    private float _bobSmoothed;
    private float _breathSmoothed;
    private float _slopeTilt;

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

    private bool GetIsGrounded() => _movement != null && _movement.IsGrounded;

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
            // Bukan landing nyata (mis. flicker grounded di tangga) -> buang airtime.
            _airTime = 0f;
        }
    }

    private void UpdatePelvisPosition()
    {
        float dt = Time.deltaTime;
        Vector2 vel = GetVelocity();
        bool grounded = GetIsGrounded();

        bool walking = grounded && (Mathf.Abs(vel.x) > 0.12f || (_movement != null && _movement.IsMoving));

        float blendStep = dt / Mathf.Max(0.01f, _bobBlendTime);
        _bobWeight = Mathf.MoveTowards(_bobWeight, walking ? 1f : 0f, blendStep);
        _breathWeight = Mathf.MoveTowards(_breathWeight, (grounded && !walking) ? 1f : 0f, blendStep);

        float cycle = _legController != null ? _legController.WalkCycleNormalized : (Time.time * 2f);
        float bobTarget = -Mathf.Abs(Mathf.Sin(cycle * Mathf.PI * 2f)) * _walkBobAmount * _bobWeight;
        _bobSmoothed = Mathf.Lerp(_bobSmoothed, bobTarget, 1f - Mathf.Exp(-dt / 0.05f));

        float breathTarget = Mathf.Sin(Time.time * _breathingFrequency) * _idleBreathingAmount * _breathWeight;
        _breathSmoothed = Mathf.Lerp(_breathSmoothed, breathTarget, 1f - Mathf.Exp(-dt / 0.08f));

        if (_currentLandingDip > 0f)
        {
            float recoveryRate = (_maxLandingDip / Mathf.Max(0.05f, _landingDipRecoverySpeed)) * dt;
            _currentLandingDip = Mathf.MoveTowards(_currentLandingDip, 0f, recoveryRate);
        }

        float targetLocalY = _initialLocalPosition.y + _bobSmoothed + _breathSmoothed - _currentLandingDip;
        Vector3 targetPos = new Vector3(_initialLocalPosition.x, targetLocalY, _initialLocalPosition.z);

        transform.localPosition = Vector3.SmoothDamp(
            transform.localPosition,
            targetPos,
            ref _positionVelocity,
            Mathf.Max(0.001f, _positionSmoothTime),
            Mathf.Infinity,
            dt);
    }

    private void UpdatePelvisRotation()
    {
        float dt = Time.deltaTime;
        Vector2 vel = GetVelocity();
        float currentVx = vel.x;
        float facingDir = _facingReference != null ? Mathf.Sign(_facingReference.lossyScale.x) : 1f;
        bool grounded = GetIsGrounded();

        float forwardLeanAngle = 0f;
        if (grounded && Mathf.Abs(currentVx) > 0.15f)
        {
            float maxSpeed = (_movement != null) ? Mathf.Max(1f, _movement.Speed) : 6f;
            float speedRatio = Mathf.Clamp01(Mathf.Abs(currentVx) / maxSpeed);

            float moveDirX = Mathf.Sign(currentVx);
            float forwardMultiplier = (moveDirX == facingDir) ? 1f : -0.7f;

            forwardLeanAngle = speedRatio * _forwardWalkLeanAngle * forwardMultiplier;
        }

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

        float slopeTarget = 0f;
        if (grounded && _legController != null && _legController.TryGetAverageGroundNormal(out Vector2 groundNormal))
        {
            float slopeAngle = Mathf.Atan2(groundNormal.x, groundNormal.y) * Mathf.Rad2Deg;
            slopeAngle = Mathf.Clamp(slopeAngle, -_maxSlopeTiltAngle, _maxSlopeTiltAngle);
            slopeTarget = slopeAngle * facingDir * _slopeTiltWeight;
        }

        _slopeTilt = Mathf.Lerp(_slopeTilt, slopeTarget, 1f - Mathf.Exp(-dt * _slopeTiltSpeed));

        float targetTotalTilt = forwardLeanAngle + inertiaTilt + _slopeTilt;
        _currentTotalTilt = Mathf.Lerp(_currentTotalTilt, targetTotalTilt, 1f - Mathf.Exp(-dt * _tiltRecoverySpeed));

        transform.localRotation = _initialLocalRotation * Quaternion.Euler(0f, 0f, -_currentTotalTilt);
    }
}
