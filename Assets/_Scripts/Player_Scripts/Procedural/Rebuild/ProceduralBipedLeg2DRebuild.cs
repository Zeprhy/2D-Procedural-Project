using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ProceduralBipedLeg2DRebuild : MonoBehaviour
{
    public enum LocomotionState
    {
        Idle,
        Walking,
        Airborne,
        Landing
    }

    public enum FootSide
    {
        Left,
        Right
    }

    private enum ActiveLeg
    {
        None,
        Left,
        Right
    }

    [Header("Target IK Kaki (Limb Solver 2D Targets)")]
    [SerializeField] private Transform _leftTarget;
    [SerializeField] private Transform _rightTarget;

    [Header("Titik Referensi Stance (Foot Homes)")]
    [SerializeField] private Transform _leftHome;
    [SerializeField] private Transform _rightHome;

    [Header("Pangkal Paha (Hip Joints)")]
    [SerializeField] private Transform _leftHip;
    [SerializeField] private Transform _rightHip;

    [Header("Referensi Fisika & Karakter")]
    [SerializeField] private PlatformerMovement _movement;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private Transform _facingReference;

    [Header("Pengaturan Langkah")]
    [SerializeField] private float _baseStepDistance = 0.42f;
    [SerializeField] private float _speedStrideMultiplier = 0.04f;
    [SerializeField] private float _stepHeight = 0.16f;
    [SerializeField] private float _stepSpeed = 11f;
    [Tooltip("Offset sol sepatu diukur sepanjang normal permukaan")]
    [SerializeField] private float _footYOffset = 0.05f;

    [Header("Stance & Batasan")]
    [SerializeField] private float _stanceWidth = 0.22f;
    [SerializeField] private float _maxLegReach = 1.15f;
    [SerializeField] private float _maxSlopeAngle = 45f;
    [SerializeField] private float _raycastDistance = 2.5f;
    [SerializeField] private float _footAlignSpeed = 15f;

    [Header("Smart Stairs & Landing")]
    [SerializeField] private float _stairTreadProbeDistance = 0.14f;
    [SerializeField] private float _landingContactTolerance = 0.06f; // Diperlonggar agar stabil di lereng
    [SerializeField] private float _landingDampSpeed = 18f;

    [Header("Stability & Anti-Jitter")]
    [SerializeField] private float _groundGraceDuration = 0.10f;
    [SerializeField] private float _airborneVelocityThreshold = 3.8f; // Dinaikkan agar kecepatan lereng tidak memicu airborne
    [SerializeField] private float _walkEnterSpeed = 0.18f;
    [SerializeField] private float _walkExitSpeed = 0.08f;
    [SerializeField] private float _walkEnterDelay = 0.05f;
    [SerializeField] private float _walkExitDelay = 0.10f;
    [SerializeField] private float _stepRetryCooldown = 0.04f;

    [Header("Step Trigger")]
    [Tooltip("Kecepatan minimum agar arah langkah mengikuti momentum")]
    [SerializeField] private float _momentumStepSpeed = 0.4f;
    [Tooltip("Rasio jarak terhadap stride untuk memicu langkah")]
    [SerializeField] private float _stepTriggerRatio = 0.70f;
    [Tooltip("Rasio jarak untuk re-arm langkah berikutnya")]
    [SerializeField] private float _stepRearmRatio = 0.40f;
    [Tooltip("Jeda minimum setelah langkah selesai sebelum langkah berikutnya")]
    [SerializeField] private float _stepCooldown = 0.05f;
    [Tooltip("Lead time prediksi pijakan berdasarkan kecepatan")]
    [SerializeField] private float _stepPredictionLead = 0.04f;
    [Tooltip("Pengurangan stride di lereng")]
    [SerializeField][Range(0f, 1f)] private float _slopeStrideScale = 0.15f;
    [Tooltip("Rotasi tambahan telapak saat ayunan")]
    [SerializeField] private float _stepSwingRoll = 0f;

    [Header("Footstep Events")]
    [SerializeField] private bool _enableFootstepEvents = true;

    [Header("Debug")]
    [SerializeField] private bool _drawDebugGizmos = true;

    public event Action<FootSide, Vector3> OnFootstep;

    public LocomotionState CurrentState { get; private set; } = LocomotionState.Idle;
    public float WalkCycleNormalized { get; private set; }

    private Vector3 _leftPlantedPos;
    private Vector3 _rightPlantedPos;
    private Quaternion _leftPlantedRot = Quaternion.identity;
    private Quaternion _rightPlantedRot = Quaternion.identity;

    private Vector3 _leftSwingStart;
    private Vector3 _leftSwingTarget;
    private Quaternion _leftSwingStartRot = Quaternion.identity;
    private Quaternion _leftTargetRot = Quaternion.identity;

    private Vector3 _rightSwingStart;
    private Vector3 _rightSwingTarget;
    private Quaternion _rightSwingStartRot = Quaternion.identity;
    private Quaternion _rightTargetRot = Quaternion.identity;

    private ActiveLeg _activeSwingLeg = ActiveLeg.None;
    private float _stepProgress;
    private bool _lastSteppedLeft;
    private float _landingTimeoutTimer;
    private float _stepRetryTimer;
    private float _stepCooldownTimer;

    private bool _leftStepArmed = true;
    private bool _rightStepArmed = true;

    private bool _isIdleSettled;
    private Vector3 _settledBodyPos;

    private float _groundGraceTimer;
    private float _walkEnterTimer;
    private float _walkExitTimer;

    private bool _isInitialized;

    private struct FootGroundSample
    {
        public bool valid;
        public Vector3 point;
        public Vector2 normal;
        public Quaternion rotation;
        public float slopeAngle;
    }

    private FootGroundSample _leftSample;
    private FootGroundSample _rightSample;

    public Vector3 LeftFootPosition => _leftTarget != null ? _leftTarget.position : _leftPlantedPos;
    public Vector3 RightFootPosition => _rightTarget != null ? _rightTarget.position : _rightPlantedPos;

    private void Awake()
    {
        if (_movement == null)
            _movement = GetComponent<PlatformerMovement>();

        if (_facingReference == null)
            _facingReference = transform;
    }

    private void Start()
    {
        InitializeStance();
    }

    private void Update()
    {
        if (!_isInitialized) return;

        float dt = Time.deltaTime;
        if (_stepRetryTimer > 0f) _stepRetryTimer -= dt;
        if (_stepCooldownTimer > 0f) _stepCooldownTimer -= dt;

        bool bodyMoved = (transform.position - _settledBodyPos).sqrMagnitude > 0.0001f;
        if (!_isIdleSettled || bodyMoved || CurrentState != LocomotionState.Idle)
            RefreshGroundSamples();

        UpdateLocomotionState();
        ProcessCurrentState();
    }

    private void LateUpdate()
    {
        if (!_isInitialized) return;
        ApplyTargetTransforms();
    }

    #region Safe Movement Queries

    private Vector2 GetVelocity()
    {
        if (_movement != null && _movement.PlayerRigidbody != null)
            return _movement.PlayerRigidbody.linearVelocity;

        return Vector2.zero;
    }

    private bool GetIsGrounded() => _movement != null && _movement.IsGrounded;

    private bool GetIsMoving()
    {
        if (_movement == null) return false;
        return Mathf.Abs(GetVelocity().x) > _walkExitSpeed || _movement.IsMoving;
    }

    #endregion

    private void InitializeStance()
    {
        if (_leftTarget == null || _rightTarget == null || _leftHome == null || _rightHome == null)
        {
            Debug.LogError("<color=red>[ProceduralBipedLeg2DRebuild]</color> Referensi Target atau Home belum lengkap!", this);
            return;
        }

        RefreshGroundSamples();

        SnapFootToGround(true, out _leftPlantedPos, out _leftPlantedRot);
        SnapFootToGround(false, out _rightPlantedPos, out _rightPlantedRot);

        _leftSwingStart = _leftPlantedPos;
        _leftSwingTarget = _leftPlantedPos;
        _leftSwingStartRot = _leftPlantedRot;
        _leftTargetRot = _leftPlantedRot;

        _rightSwingStart = _rightPlantedPos;
        _rightSwingTarget = _rightPlantedPos;
        _rightSwingStartRot = _rightPlantedRot;
        _rightTargetRot = _rightPlantedRot;

        _isIdleSettled = true;
        _settledBodyPos = transform.position;
        ApplyTargetTransforms();
        _isInitialized = true;
    }

    #region Ground Sampling

    private void RefreshGroundSamples()
    {
        _leftSample = SampleFootGround(true);
        _rightSample = SampleFootGround(false);
    }

    private FootGroundSample SampleFootGround(bool isLeft)
    {
        FootGroundSample sample = default;
        Transform home = isLeft ? _leftHome : _rightHome;
        if (home == null) return sample;

        Vector3 origin = home.position + Vector3.up * 0.5f;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, _raycastDistance, _groundLayer);
        if (hit.collider == null) return sample;

        float slope = Vector2.Angle(hit.normal, Vector2.up);
        if (slope > _maxSlopeAngle) return sample;

        Vector3 point = (Vector3)hit.point + (Vector3)(hit.normal * _footYOffset);
        point.z = home.position.z;

        sample.valid = true;
        sample.point = point;
        sample.normal = hit.normal;
        sample.rotation = CalculateFootRotation(hit.normal);
        sample.slopeAngle = slope;
        return sample;
    }

    private float GetAverageSlope()
    {
        float sum = 0f;
        int count = 0;
        if (_leftSample.valid) { sum += _leftSample.slopeAngle; count++; }
        if (_rightSample.valid) { sum += _rightSample.slopeAngle; count++; }
        return count > 0 ? sum / count : 0f;
    }

    public bool TryGetAverageGroundNormal(out Vector2 normal)
    {
        Vector2 sum = Vector2.zero;
        int count = 0;
        if (_leftSample.valid) { sum += _leftSample.normal; count++; }
        if (_rightSample.valid) { sum += _rightSample.normal; count++; }

        if (count == 0)
        {
            normal = Vector2.up;
            return false;
        }

        normal = (sum / count).normalized;
        return true;
    }

    #endregion

    private void UpdateLocomotionState()
    {
        bool grounded = GetIsGrounded();
        Vector2 vel = GetVelocity();
        float dt = Time.deltaTime;

        if (grounded)
            _groundGraceTimer = 0f;
        else if (CurrentState != LocomotionState.Airborne)
            _groundGraceTimer += dt;

        // PERBAIKAN 1: Filter Airborne Cerdas (Jangan trigger airborne jika masih menempel di tanah lereng)
        bool hasGroundSample = _leftSample.valid || _rightSample.valid;
        bool isTrulyAirborne = !grounded &&
            (CurrentState == LocomotionState.Airborne ||
             _groundGraceTimer >= _groundGraceDuration ||
             (vel.y < -_airborneVelocityThreshold && !hasGroundSample));

        if (isTrulyAirborne)
        {
            if (CurrentState != LocomotionState.Airborne)
            {
                CurrentState = LocomotionState.Airborne;
                _activeSwingLeg = ActiveLeg.None;
                _stepProgress = 0f;
                _isIdleSettled = false;
            }
            return;
        }

        if (CurrentState == LocomotionState.Airborne && grounded)
        {
            CurrentState = LocomotionState.Landing;
            _landingTimeoutTimer = 0.35f;
            _isIdleSettled = false;
            return;
        }

        if (CurrentState == LocomotionState.Landing)
        {
            _landingTimeoutTimer -= dt;

            bool validL = _leftSample.valid;
            bool validR = _rightSample.valid;
            float dyL = validL ? Mathf.Abs(_leftPlantedPos.y - _leftSample.point.y) : float.PositiveInfinity;
            float dyR = validR ? Mathf.Abs(_rightPlantedPos.y - _rightSample.point.y) : float.PositiveInfinity;

            bool feetGrounded = (validL && dyL <= _landingContactTolerance) || 
                                (validR && dyR <= _landingContactTolerance);

            // PERBAIKAN 2: Begitu landing selesai/timeout, snap kaki ke tanah valid agar tidak menggantung di udara
            if (feetGrounded || _landingTimeoutTimer <= 0f)
            {
                if (validL) _leftPlantedPos = _leftSample.point;
                if (validR) _rightPlantedPos = _rightSample.point;

                CurrentState = GetIsMoving() ? LocomotionState.Walking : LocomotionState.Idle;
                _isIdleSettled = false;
            }
            return;
        }

        float absVx = Mathf.Abs(vel.x);
        bool inputMoving = _movement != null && _movement.IsMoving;
        bool wantsWalk = inputMoving || absVx > _walkEnterSpeed;
        bool wantsIdle = !inputMoving && absVx < _walkExitSpeed;

        LocomotionState targetState = CurrentState;

        if (CurrentState == LocomotionState.Walking)
        {
            if (wantsIdle) _walkExitTimer += dt;
            else _walkExitTimer = 0f;

            if (_walkExitTimer >= _walkExitDelay)
            {
                targetState = LocomotionState.Idle;
                _walkExitTimer = 0f;
            }
        }
        else
        {
            if (wantsWalk) _walkEnterTimer += dt;
            else _walkEnterTimer = 0f;

            if (_walkEnterTimer >= _walkEnterDelay)
            {
                targetState = LocomotionState.Walking;
                _walkEnterTimer = 0f;
            }
        }

        if (CurrentState != targetState)
        {
            CurrentState = targetState;
            if (CurrentState == LocomotionState.Idle)
                _isIdleSettled = false;

            _walkEnterTimer = 0f;
            _walkExitTimer = 0f;
        }
    }

    private void ProcessCurrentState()
    {
        switch (CurrentState)
        {
            case LocomotionState.Idle:
                ProcessIdleState();
                break;
            case LocomotionState.Walking:
                ProcessWalkingState();
                break;
            case LocomotionState.Airborne:
                ProcessAirborneState();
                break;
            case LocomotionState.Landing:
                ProcessLandingState();
                break;
        }
    }

    private void ProcessIdleState()
    {
        if (_activeSwingLeg != ActiveLeg.None)
        {
            ProgressSwingStep();
            return;
        }

        if (_isIdleSettled)
        {
            if ((transform.position - _settledBodyPos).sqrMagnitude > 0.0001f)
                _isIdleSettled = false;
            else
            {
                WalkCycleNormalized = 0f;
                return;
            }
        }

        SnapFootToGround(true, out Vector3 targetLeft, out Quaternion rotLeft);
        SnapFootToGround(false, out Vector3 targetRight, out Quaternion rotRight);

        _leftPlantedPos = Vector3.Lerp(_leftPlantedPos, targetLeft, Time.deltaTime * 12f);
        _leftPlantedRot = Quaternion.Slerp(_leftPlantedRot, rotLeft, Time.deltaTime * _footAlignSpeed);

        _rightPlantedPos = Vector3.Lerp(_rightPlantedPos, targetRight, Time.deltaTime * 12f);
        _rightPlantedRot = Quaternion.Slerp(_rightPlantedRot, rotRight, Time.deltaTime * _footAlignSpeed);

        float diffL = (targetLeft - _leftPlantedPos).sqrMagnitude;
        float diffR = (targetRight - _rightPlantedPos).sqrMagnitude;

        if (diffL < 0.0001f && diffR < 0.0001f)
        {
            _leftPlantedPos = targetLeft;
            _rightPlantedPos = targetRight;
            _leftPlantedRot = rotLeft;
            _rightPlantedRot = rotRight;
            _isIdleSettled = true;
            _settledBodyPos = transform.position;
        }

        WalkCycleNormalized = 0f;
    }

    private void ProcessWalkingState()
    {
        _isIdleSettled = false;

        if (_activeSwingLeg != ActiveLeg.None)
        {
            ProgressSwingStep();
            return;
        }

        Vector2 vel = GetVelocity();
        float vx = vel.x;
        float facingDir = _facingReference != null ? Mathf.Sign(_facingReference.lossyScale.x) : 1f;

        float moveDir;
        if (Mathf.Abs(vx) > _momentumStepSpeed)
            moveDir = Mathf.Sign(vx);
        else if (_movement != null && Mathf.Abs(_movement.PlayerRigidbody.linearVelocity.x) > 0.02f)
            moveDir = Mathf.Sign(_movement.PlayerRigidbody.linearVelocity.x);
        else
            moveDir = facingDir;

        float slopeFactor = Mathf.Lerp(1f, 1f - _slopeStrideScale, GetAverageSlope() / Mathf.Max(1f, _maxSlopeAngle));
        float dynamicStride = (_baseStepDistance + (Mathf.Abs(vx) * _speedStrideMultiplier)) * slopeFactor;
        
        // Sedikit perpendek langkah jika berjalan mundur
        if (moveDir != facingDir) dynamicStride *= 0.80f;

        Vector3 idealLeft = CalculateIdealStancePos(true);
        Vector3 idealRight = CalculateIdealStancePos(false);

        float leftDist = Mathf.Abs(_leftPlantedPos.x - idealLeft.x);
        float rightDist = Mathf.Abs(_rightPlantedPos.x - idealRight.x);

        // PERBAIKAN 3: Update Rearm dengan pengaman darurat anti-terkunci
        UpdateStepRearm(leftDist, rightDist, dynamicStride);

        // PERBAIKAN 4: Emergency Step jika kaki tertinggal terlalu jauh (mencegah kaki terseret/terangkat saat mundur)
        float maxAllowedDrag = _maxLegReach * 0.85f;
        if (Vector3.Distance(_leftPlantedPos, _leftHip.position) > maxAllowedDrag)
        {
            _leftStepArmed = true;
            if (TryTriggerStep(true, leftDist, moveDir, dynamicStride)) return;
        }
        if (Vector3.Distance(_rightPlantedPos, _rightHip.position) > maxAllowedDrag)
        {
            _rightStepArmed = true;
            if (TryTriggerStep(false, rightDist, moveDir, dynamicStride)) return;
        }

        if (_stepCooldownTimer > 0f) return;

        if (!_lastSteppedLeft)
        {
            if (TryTriggerStep(true, leftDist, moveDir, dynamicStride)) return;
            TryTriggerStep(false, rightDist, moveDir, dynamicStride);
        }
        else
        {
            if (TryTriggerStep(false, rightDist, moveDir, dynamicStride)) return;
            TryTriggerStep(true, leftDist, moveDir, dynamicStride);
        }
    }

    private void UpdateStepRearm(float leftDist, float rightDist, float stride)
    {
        float rearm = stride * _stepRearmRatio;

        // Rearm normal saat kaki mendekat, ATAU rearm darurat saat kaki tertinggal jauh (mencegah deadlock mundur)
        if (leftDist <= rearm || leftDist > stride * 0.95f) _leftStepArmed = true;
        if (rightDist <= rearm || rightDist > stride * 0.95f) _rightStepArmed = true;
    }

    private bool TryTriggerStep(bool isLeft, float dist, float moveDir, float stride)
    {
        if (dist <= stride * _stepTriggerRatio) return false;

        bool armed = isLeft ? _leftStepArmed : _rightStepArmed;
        if (!armed) return false;

        if (!TriggerStep(isLeft, moveDir, stride)) return false;

        if (isLeft) _leftStepArmed = false;
        else _rightStepArmed = false;
        return true;
    }

    private bool TriggerStep(bool isLeft, float moveDir, float stride)
    {
        if (_stepRetryTimer > 0f) return false;

        Vector3 bodyPos = transform.position;
        float vx = GetVelocity().x;

        Transform home = isLeft ? _leftHome : _rightHome;
        float sideOffset = home != null
            ? home.position.x - bodyPos.x
            : (isLeft ? -1f : 1f) * (_stanceWidth * 0.5f);

        // Batasi lead prediksi agar tidak melempar kaki terlalu jauh keluar jangkauan paha saat mundur
        float leadSpeed = Mathf.Clamp(Mathf.Abs(vx) * _stepPredictionLead, 0f, 0.12f);
        float predictedX = bodyPos.x + sideOffset + (moveDir * (stride + leadSpeed));

        Vector3 rayOrigin = new Vector3(predictedX, bodyPos.y + 1.2f, bodyPos.z);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, _raycastDistance + 1.2f, _groundLayer);

        bool validTreadFound = false;
        Vector3 landingPos = Vector3.zero;
        Quaternion landingRot = Quaternion.identity;

        if (hit.collider != null)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

            if (slopeAngle <= _maxSlopeAngle)
            {
                landingPos = (Vector3)hit.point + (Vector3)(hit.normal * _footYOffset);
                landingPos.z = bodyPos.z;
                landingRot = CalculateFootRotation(hit.normal);
                validTreadFound = true;
            }
            else
            {
                Vector3 probeOrigin = new Vector3(predictedX + (moveDir * _stairTreadProbeDistance), bodyPos.y + 1.5f, bodyPos.z);
                RaycastHit2D hitTread = Physics2D.Raycast(probeOrigin, Vector2.down, _raycastDistance + 1.5f, _groundLayer);

                if (hitTread.collider != null && Vector2.Angle(hitTread.normal, Vector2.up) <= _maxSlopeAngle)
                {
                    landingPos = (Vector3)hitTread.point + (Vector3)(hitTread.normal * _footYOffset);
                    landingPos.z = bodyPos.z;
                    landingRot = CalculateFootRotation(hitTread.normal);
                    validTreadFound = true;
                }
            }
        }

        if (!validTreadFound)
        {
            _stepRetryTimer = _stepRetryCooldown;
            return false;
        }

        Transform hip = isLeft ? _leftHip : _rightHip;
        landingPos = ClampHyperextensionSmart(landingPos, hip);

        if (isLeft)
        {
            _leftSwingStart = _leftPlantedPos;
            _leftSwingStartRot = _leftPlantedRot;
            _leftSwingTarget = landingPos;
            _leftTargetRot = landingRot;
        }
        else
        {
            _rightSwingStart = _rightPlantedPos;
            _rightSwingStartRot = _rightPlantedRot;
            _rightSwingTarget = landingPos;
            _rightTargetRot = landingRot;
        }

        _activeSwingLeg = isLeft ? ActiveLeg.Left : ActiveLeg.Right;
        _stepProgress = 0f;
        return true;
    }

    private void ProgressSwingStep()
    {
        _stepProgress += Time.deltaTime * _stepSpeed;
        float t = Mathf.Clamp01(_stepProgress);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        bool isLeft = _activeSwingLeg == ActiveLeg.Left;
        float targetY = isLeft ? _leftSwingTarget.y : _rightSwingTarget.y;
        float startY = isLeft ? _leftSwingStart.y : _rightSwingStart.y;
        float elevationDelta = targetY - startY;

        float currentStepHeight = _stepHeight;
        float arcFactor;

        if (elevationDelta > 0.06f)
        {
            currentStepHeight += Mathf.Clamp(elevationDelta * 0.5f, 0f, 0.22f);
            arcFactor = Mathf.Sin(Mathf.Pow(t, 0.7f) * Mathf.PI);
        }
        else
        {
            arcFactor = Mathf.Sin(t * Mathf.PI);
        }

        float facingDir = _facingReference != null ? Mathf.Sign(_facingReference.lossyScale.x) : 1f;
        float roll = Mathf.Sin(t * Mathf.PI) * _stepSwingRoll * facingDir;

        if (isLeft)
        {
            Vector3 pos = Vector3.Lerp(_leftSwingStart, _leftSwingTarget, smoothT);
            pos.y += arcFactor * currentStepHeight;
            _leftPlantedPos = pos;
            _leftPlantedRot = Quaternion.Slerp(_leftSwingStartRot, _leftTargetRot, smoothT) * Quaternion.Euler(0f, 0f, roll);
            WalkCycleNormalized = t * 0.5f;
        }
        else
        {
            Vector3 pos = Vector3.Lerp(_rightSwingStart, _rightSwingTarget, smoothT);
            pos.y += arcFactor * currentStepHeight;
            _rightPlantedPos = pos;
            _rightPlantedRot = Quaternion.Slerp(_rightSwingStartRot, _rightTargetRot, smoothT) * Quaternion.Euler(0f, 0f, roll);
            WalkCycleNormalized = 0.5f + (t * 0.5f);
        }

        if (t >= 1f)
        {
            if (isLeft)
            {
                _leftPlantedPos = _leftSwingTarget;
                _leftPlantedRot = _leftTargetRot;
                _lastSteppedLeft = true;
            }
            else
            {
                _rightPlantedPos = _rightSwingTarget;
                _rightPlantedRot = _rightTargetRot;
                _lastSteppedLeft = false;
            }

            Vector3 landedPos = isLeft ? _leftPlantedPos : _rightPlantedPos;
            if (_enableFootstepEvents)
                OnFootstep?.Invoke(isLeft ? FootSide.Left : FootSide.Right, landedPos);

            _stepCooldownTimer = _stepCooldown;
            _activeSwingLeg = ActiveLeg.None;
            _stepProgress = 0f;
        }
    }

    private void ProcessAirborneState()
    {
        _activeSwingLeg = ActiveLeg.None;
        _stepProgress = 0f;
        _isIdleSettled = false;

        Vector3 targetLeft = _leftHome != null ? _leftHome.position : transform.position;
        Vector3 targetRight = _rightHome != null ? _rightHome.position : transform.position;

        float vy = GetVelocity().y;
        if (vy < -2.0f)
        {
            float legDrop = Mathf.Clamp(-vy * 0.025f, 0f, 0.18f);
            targetLeft.y -= legDrop;
            targetRight.y -= legDrop;
        }

        _leftPlantedPos = Vector3.Lerp(_leftPlantedPos, targetLeft, Time.deltaTime * 12f);
        _rightPlantedPos = Vector3.Lerp(_rightPlantedPos, targetRight, Time.deltaTime * 12f);

        _leftPlantedRot = Quaternion.Slerp(_leftPlantedRot, Quaternion.identity, Time.deltaTime * 10f);
        _rightPlantedRot = Quaternion.Slerp(_rightPlantedRot, Quaternion.identity, Time.deltaTime * 10f);

        WalkCycleNormalized = 0f;
    }

    private void ProcessLandingState()
    {
        SnapFootToGround(true, out Vector3 targetLeft, out Quaternion rotLeft);
        SnapFootToGround(false, out Vector3 targetRight, out Quaternion rotRight);

        _leftPlantedPos = Vector3.MoveTowards(_leftPlantedPos, targetLeft, _landingDampSpeed * Time.deltaTime);
        _rightPlantedPos = Vector3.MoveTowards(_rightPlantedPos, targetRight, _landingDampSpeed * Time.deltaTime);

        _leftPlantedRot = Quaternion.Slerp(_leftPlantedRot, rotLeft, Time.deltaTime * 20f);
        _rightPlantedRot = Quaternion.Slerp(_rightPlantedRot, rotRight, Time.deltaTime * 20f);
    }

    private Vector3 CalculateIdealStancePos(bool isLeft)
    {
        FootGroundSample sample = isLeft ? _leftSample : _rightSample;
        if (sample.valid) return sample.point;

        Transform home = isLeft ? _leftHome : _rightHome;
        return home != null ? home.position : transform.position;
    }

    private bool SnapFootToGround(bool isLeft, out Vector3 groundPos, out Quaternion groundRot)
    {
        FootGroundSample sample = isLeft ? _leftSample : _rightSample;
        Transform hip = isLeft ? _leftHip : _rightHip;
        Transform home = isLeft ? _leftHome : _rightHome;

        if (sample.valid)
        {
            groundPos = ClampHyperextensionSmart(sample.point, hip);
            groundRot = sample.rotation;
            return true;
        }

        if (_isInitialized)
        {
            groundPos = isLeft ? _leftPlantedPos : _rightPlantedPos;
            groundRot = isLeft ? _leftPlantedRot : _rightPlantedRot;
        }
        else
        {
            groundPos = home != null ? home.position : transform.position;
            groundRot = Quaternion.identity;
        }
        return false;
    }

    private Quaternion CalculateFootRotation(Vector2 normal)
    {
        if (normal.y > 0.3f)
        {
            float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
            return Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, -_maxSlopeAngle, _maxSlopeAngle));
        }
        return Quaternion.identity;
    }

    private Vector3 ClampHyperextensionSmart(Vector3 targetPos, Transform hip)
    {
        if (hip == null) return targetPos;

        Vector3 hipToTarget = targetPos - hip.position;
        float dist = hipToTarget.magnitude;
        float allowedReach = _maxLegReach * 1.15f;

        if (dist <= allowedReach || dist <= 0.001f)
            return targetPos;

        Vector3 clampedDir = hipToTarget / dist;
        float clampedX = hip.position.x + (clampedDir.x * allowedReach);

        Vector3 reRayOrigin = new Vector3(clampedX, hip.position.y + 0.5f, targetPos.z);
        RaycastHit2D reHit = Physics2D.Raycast(reRayOrigin, Vector2.down, _raycastDistance + 1f, _groundLayer);

        if (reHit.collider != null && Vector2.Angle(reHit.normal, Vector2.up) <= _maxSlopeAngle)
        {
            Vector3 rePoint = (Vector3)reHit.point + (Vector3)(reHit.normal * _footYOffset);
            Vector3 reValidatedPos = new Vector3(clampedX, rePoint.y, targetPos.z);

            if ((reValidatedPos - hip.position).magnitude <= allowedReach * 1.08f)
                return reValidatedPos;
        }

        return targetPos;
    }

    private void ApplyTargetTransforms()
    {
        if (_leftTarget != null)
        {
            _leftTarget.position = _leftPlantedPos;
            _leftTarget.rotation = _leftPlantedRot;
        }

        if (_rightTarget != null)
        {
            _rightTarget.position = _rightPlantedPos;
            _rightTarget.rotation = _rightPlantedRot;
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !_drawDebugGizmos) return;

        Gizmos.color = (_activeSwingLeg == ActiveLeg.Left) ? Color.magenta : Color.cyan;
        Gizmos.DrawWireSphere(_leftPlantedPos, 0.06f);

        Gizmos.color = (_activeSwingLeg == ActiveLeg.Right) ? Color.magenta : Color.yellow;
        Gizmos.DrawWireSphere(_rightPlantedPos, 0.06f);

        Gizmos.color = _leftSample.valid ? Color.green : Color.red;
        Gizmos.DrawWireSphere(_leftSample.point, 0.035f);

        Gizmos.color = _rightSample.valid ? Color.green : Color.red;
        Gizmos.DrawWireSphere(_rightSample.point, 0.035f);
    }
}