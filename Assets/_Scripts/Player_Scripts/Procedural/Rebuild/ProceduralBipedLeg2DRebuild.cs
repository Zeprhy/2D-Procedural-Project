using UnityEngine;
using System;

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

    [Header("Pangkal Paha (Hip Joints) — Hyperextension Guard")]
    [SerializeField] private Transform _leftHip;
    [SerializeField] private Transform _rightHip;

    [Header("Referensi Fisika & Karakter")]
    [SerializeField] private PlatformerMovement _movement;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private Transform _facingReference;

    [Header("Pengaturan Langkah Prosedural")]
    [Tooltip("Panjang langkah horizontal dasar")]
    [SerializeField] private float _baseStepDistance = 0.42f;
    [Tooltip("Pengali panjang langkah berdasarkan kecepatan gerak")]
    [SerializeField] private float _speedStrideMultiplier = 0.04f;
    [Tooltip("Tinggi lengkungan ayunan kaki")]
    [SerializeField] private float _stepHeight = 0.16f;
    [Tooltip("Kecepatan transisi ayunan langkah")]
    [SerializeField] private float _stepSpeed = 11f;
    [Tooltip("Jarak vertikal sol sepatu ke collider tanah")]
    [SerializeField] private float _footYOffset = 0.05f;

    [Header("Stance & Batasan")]
    [Tooltip("Lebar stance lateral antar kaki kiri dan kanan")]
    [SerializeField] private float _stanceWidth = 0.22f;
    [Tooltip("Panjang jangkauan kaki maksimum dari hip sebelum ditarik")]
    [SerializeField] private float _maxLegReach = 1.15f;
    [Tooltip("Sudut kemiringan lereng maksimum yang valid")]
    [SerializeField] private float _maxSlopeAngle = 45f;
    [Tooltip("Jarak raycast tanah")]
    [SerializeField] private float _raycastDistance = 2.5f;
    [Tooltip("Kecepatan penyesuaian rotasi telapak kaki ke normal permukaan tanah")]
    [SerializeField] private float _footAlignSpeed = 15f;

    [Header("Smart Stairs & Landing Tuning")]
    [Tooltip("Jarak probe maju ke depan saat raycast pertama membentur dinding anak tangga")]
    [SerializeField] private float _stairTreadProbeDistance = 0.14f;
    [Tooltip("Toleransi jarak kontak tanah saat pendaratan")]
    [SerializeField] private float _landingContactTolerance = 0.025f;
    [Tooltip("Kecepatan peredaman pendaratan menuju tanah")]
    [SerializeField] private float _landingDampSpeed = 16f;

    // State Tracking
    public LocomotionState CurrentState { get; private set; } = LocomotionState.Idle;
    public float WalkCycleNormalized { get; private set; }

    private Vector3 _leftPlantedPos;
    private Vector3 _rightPlantedPos;
    private Quaternion _leftPlantedRot = Quaternion.identity;
    private Quaternion _rightPlantedRot = Quaternion.identity;

    private Vector3 _leftSwingStart;
    private Vector3 _leftSwingTarget;
    private Quaternion _leftTargetRot;

    private Vector3 _rightSwingStart;
    private Vector3 _rightSwingTarget;
    private Quaternion _rightTargetRot;

    private ActiveLeg _activeSwingLeg = ActiveLeg.None;
    private float _stepProgress = 0f;
    private bool _lastSteppedLeft = false;
    private float _landingTimeoutTimer = 0f;

    // Flag Penstabil Pijakan Idle (Anti-Jitter Lock)
    private bool _isIdleSettled = false;

    private bool _leftIsGrounded;
    private bool _rightIsGrounded;
    private bool _isInitialized = false;

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

    private bool GetIsGrounded()
    {
        return _movement != null && _movement.IsGrounded;
    }

    private bool GetIsMoving()
    {
        if (_movement == null) return false;
        float vx = Mathf.Abs(GetVelocity().x);
        return vx > 0.12f || _movement.IsMoving;
    }

    #endregion

    private void InitializeStance()
    {
        if (_leftTarget == null || _rightTarget == null || _leftHome == null || _rightHome == null)
        {
            Debug.LogError("<color=red>[ProceduralBipedLeg2DRebuild]</color> Referensi Target atau Home belum lengkap!", this);
            return;
        }

        SnapFootToGround(true, out _leftPlantedPos, out _leftPlantedRot);
        SnapFootToGround(false, out _rightPlantedPos, out _rightPlantedRot);

        _leftSwingStart = _leftPlantedPos;
        _leftSwingTarget = _leftPlantedPos;
        _rightSwingStart = _rightPlantedPos;
        _rightSwingTarget = _rightPlantedPos;

        _isIdleSettled = true;
        ApplyTargetTransforms();
        _isInitialized = true;
    }

    private void UpdateLocomotionState()
    {
        bool grounded = GetIsGrounded();
        Vector2 vel = GetVelocity();

        // 1. FILTER AIRBORNE CERDAS (Mencegah flicker 1 frame di lereng saat idle)
        bool isTrulyAirborne = !grounded;
        if (CurrentState == LocomotionState.Idle && !grounded)
        {
            // Jika sedang idle di lereng dan ada flicker tanah, hanya anggap airborne jika ada kecepatan vertikal jatuh/lompat
            if (Mathf.Abs(vel.y) < 1.0f)
            {
                isTrulyAirborne = false;
            }
        }

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

        // 2. TRANSISI MENUJU LANDING
        if (CurrentState == LocomotionState.Airborne && grounded)
        {
            CurrentState = LocomotionState.Landing;
            _landingTimeoutTimer = 0.35f;
            _isIdleSettled = false;
            return;
        }

        // 3. EVALUASI STATE LANDING
        if (CurrentState == LocomotionState.Landing)
        {
            _landingTimeoutTimer -= Time.deltaTime;

            SnapFootToGround(true, out Vector3 targetLeft, out _);
            SnapFootToGround(false, out Vector3 targetRight, out _);

            float distLeft = Vector3.Distance(_leftPlantedPos, targetLeft);
            float distRight = Vector3.Distance(_rightPlantedPos, targetRight);

            bool feetGrounded = distLeft <= _landingContactTolerance && distRight <= _landingContactTolerance;
            bool timedOut = _landingTimeoutTimer <= 0f;

            if (feetGrounded || timedOut)
            {
                CurrentState = GetIsMoving() ? LocomotionState.Walking : LocomotionState.Idle;
                _isIdleSettled = false;
            }
            return;
        }

        // 4. EVALUASI WALKING VS IDLE
        bool isMoving = GetIsMoving();
        LocomotionState targetState = isMoving ? LocomotionState.Walking : LocomotionState.Idle;

        if (CurrentState != targetState)
        {
            CurrentState = targetState;
            if (CurrentState == LocomotionState.Idle)
            {
                _isIdleSettled = false; // Reset agar kaki menyelesaikan settle sekali saat berhenti
            }
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

    /// <summary>
    /// PHASE 5.1: Kaki menyesuaikan diri ke tanah sekali saat berhenti, lalu DIBEKUKAN TOTAL agar tidak bergetar di lereng.
    /// </summary>
    private void ProcessIdleState()
    {
        // Jika masih ada langkah ayunan yang belum selesai saat berhenti, biarkan mendarat mulus
        if (_activeSwingLeg != ActiveLeg.None)
        {
            ProgressSwingStep();
            return;
        }

        // Jika kedua kaki sudah terkunci diam di lereng, JANGAN LERP ATAU RAYCAST LAGI!
        if (_isIdleSettled)
        {
            WalkCycleNormalized = 0f;
            return;
        }

        // Transisi halus saat baru berhenti menuju kontak tanah stabil
        SnapFootToGround(true, out Vector3 targetLeft, out Quaternion rotLeft);
        SnapFootToGround(false, out Vector3 targetRight, out Quaternion rotRight);

        _leftPlantedPos = Vector3.Lerp(_leftPlantedPos, targetLeft, Time.deltaTime * 12f);
        _leftPlantedRot = Quaternion.Slerp(_leftPlantedRot, rotLeft, Time.deltaTime * _footAlignSpeed);

        _rightPlantedPos = Vector3.Lerp(_rightPlantedPos, targetRight, Time.deltaTime * 12f);
        _rightPlantedRot = Quaternion.Slerp(_rightPlantedRot, rotRight, Time.deltaTime * _footAlignSpeed);

        // Kunci posisi jika selisih jarak sudah sangat kecil (< 3 milimeter)
        float diffL = (targetLeft - _leftPlantedPos).sqrMagnitude;
        float diffR = (targetRight - _rightPlantedPos).sqrMagnitude;

        if (diffL < 0.0001f && diffR < 0.0001f)
        {
            _leftPlantedPos = targetLeft;
            _rightPlantedPos = targetRight;
            _leftPlantedRot = rotLeft;
            _rightPlantedRot = rotRight;
            _isIdleSettled = true; // KUNCI! Hentikan seluruh update per frame selama diam.
        }

        WalkCycleNormalized = 0f;
    }

    private void ProcessWalkingState()
    {
        _isIdleSettled = false; // Buka kuncian idle karena karakter sedang berjalan

        if (_activeSwingLeg != ActiveLeg.None)
        {
            ProgressSwingStep();
            return;
        }

        Vector2 vel = GetVelocity();
        float vx = vel.x;
        float facingDir = _facingReference != null ? Mathf.Sign(_facingReference.lossyScale.x) : 1f;
        float moveDir = Mathf.Abs(vx) > 0.1f ? Mathf.Sign(vx) : facingDir;

        float dynamicStride = _baseStepDistance + (Mathf.Abs(vx) * _speedStrideMultiplier);
        if (moveDir != facingDir) dynamicStride *= 0.75f;

        Vector3 idealLeft = CalculateIdealStancePos(true);
        Vector3 idealRight = CalculateIdealStancePos(false);

        float leftDist = Vector2.Distance(new Vector2(_leftPlantedPos.x, _leftPlantedPos.y), new Vector2(idealLeft.x, idealLeft.y));
        float rightDist = Vector2.Distance(new Vector2(_rightPlantedPos.x, _rightPlantedPos.y), new Vector2(idealRight.x, idealRight.y));

        if (!_lastSteppedLeft)
        {
            if (leftDist > dynamicStride * 0.75f)
                TriggerStep(true, moveDir, dynamicStride);
            else if (rightDist > dynamicStride * 0.75f)
                TriggerStep(false, moveDir, dynamicStride);
        }
        else
        {
            if (rightDist > dynamicStride * 0.75f)
                TriggerStep(false, moveDir, dynamicStride);
            else if (leftDist > dynamicStride * 0.75f)
                TriggerStep(true, moveDir, dynamicStride);
        }
    }

    private void TriggerStep(bool isLeft, float moveDir, float stride)
    {
        _activeSwingLeg = isLeft ? ActiveLeg.Left : ActiveLeg.Right;
        _stepProgress = 0f;

        Vector3 bodyPos = transform.position;
        float sideOffset = (isLeft ? -1f : 1f) * (_stanceWidth * 0.5f);
        float predictedX = bodyPos.x + sideOffset + (moveDir * stride);

        Vector3 rayOrigin = new Vector3(predictedX, bodyPos.y + 1.2f, bodyPos.z);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, _raycastDistance + 1.2f, _groundLayer);

        Vector3 landingPos;
        Quaternion landingRot = Quaternion.identity;
        bool validTreadFound = false;

        if (hit.collider != null)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

            if (slopeAngle <= _maxSlopeAngle)
            {
                landingPos = new Vector3(hit.point.x, hit.point.y + _footYOffset, bodyPos.z);
                landingRot = CalculateFootRotation(hit.normal);
                validTreadFound = true;
            }
            else if (slopeAngle > 50f)
            {
                Vector3 forwardProbeOrigin = new Vector3(predictedX + (moveDir * _stairTreadProbeDistance), bodyPos.y + 1.5f, bodyPos.z);
                RaycastHit2D hitTread = Physics2D.Raycast(forwardProbeOrigin, Vector2.down, _raycastDistance + 1.5f, _groundLayer);

                if (hitTread.collider != null && Vector2.Angle(hitTread.normal, Vector2.up) <= _maxSlopeAngle)
                {
                    landingPos = new Vector3(hitTread.point.x, hitTread.point.y + _footYOffset, bodyPos.z);
                    landingRot = CalculateFootRotation(hitTread.normal);
                    validTreadFound = true;
                }
                else
                {
                    float fallbackY = isLeft ? _rightPlantedPos.y : _leftPlantedPos.y;
                    landingPos = new Vector3(predictedX, fallbackY, bodyPos.z);
                }
            }
            else
            {
                float fallbackY = isLeft ? _rightPlantedPos.y : _leftPlantedPos.y;
                landingPos = new Vector3(predictedX, fallbackY, bodyPos.z);
            }
        }
        else
        {
            float fallbackY = isLeft ? _rightPlantedPos.y : _leftPlantedPos.y;
            landingPos = new Vector3(predictedX, fallbackY, bodyPos.z);
        }

        Transform hip = isLeft ? _leftHip : _rightHip;
        landingPos = ClampHyperextensionSmart(landingPos, hip, validTreadFound);

        if (isLeft)
        {
            _leftSwingStart = _leftPlantedPos;
            _leftSwingTarget = landingPos;
            _leftTargetRot = landingRot;
        }
        else
        {
            _rightSwingStart = _rightPlantedPos;
            _rightSwingTarget = landingPos;
            _rightTargetRot = landingRot;
        }
    }

    private void ProgressSwingStep()
    {
        _stepProgress += Time.deltaTime * _stepSpeed;
        float t = Mathf.Clamp01(_stepProgress);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        float targetY = (_activeSwingLeg == ActiveLeg.Left) ? _leftSwingTarget.y : _rightSwingTarget.y;
        float startY = (_activeSwingLeg == ActiveLeg.Left) ? _leftSwingStart.y : _rightSwingStart.y;
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

        float arcY = arcFactor * currentStepHeight;

        if (_activeSwingLeg == ActiveLeg.Left)
        {
            Vector3 pos = Vector3.Lerp(_leftSwingStart, _leftSwingTarget, smoothT);
            pos.y += arcY;
            _leftPlantedPos = pos;
            _leftPlantedRot = Quaternion.Slerp(_leftPlantedRot, _leftTargetRot, smoothT);
            WalkCycleNormalized = t * 0.5f;
        }
        else
        {
            Vector3 pos = Vector3.Lerp(_rightSwingStart, _rightSwingTarget, smoothT);
            pos.y += arcY;
            _rightPlantedPos = pos;
            _rightPlantedRot = Quaternion.Slerp(_rightPlantedRot, _rightTargetRot, smoothT);
            WalkCycleNormalized = 0.5f + (t * 0.5f);
        }

        if (t >= 1f)
        {
            if (_activeSwingLeg == ActiveLeg.Left)
            {
                _leftPlantedPos = _leftSwingTarget;
                _lastSteppedLeft = true;
            }
            else
            {
                _rightPlantedPos = _rightSwingTarget;
                _lastSteppedLeft = false;
            }

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
        Vector3 origin = isLeft ? _leftHome.position : _rightHome.position;
        RaycastHit2D hit = Physics2D.Raycast(origin + Vector3.up * 0.5f, Vector2.down, _raycastDistance, _groundLayer);

        if (hit.collider != null)
        {
            return new Vector3(hit.point.x, hit.point.y + _footYOffset, origin.z);
        }

        return origin;
    }

    private bool SnapFootToGround(bool isLeft, out Vector3 groundPos, out Quaternion groundRot)
    {
        Transform home = isLeft ? _leftHome : _rightHome;
        Transform hip = isLeft ? _leftHip : _rightHip;

        Vector3 origin = home != null ? home.position : transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin + Vector3.up * 0.5f, Vector2.down, _raycastDistance, _groundLayer);

        if (hit.collider != null && Vector2.Angle(hit.normal, Vector2.up) <= _maxSlopeAngle)
        {
            Vector3 point = new Vector3(hit.point.x, hit.point.y + _footYOffset, origin.z);
            point = ClampHyperextensionSmart(point, hip, true);

            groundPos = point;
            groundRot = CalculateFootRotation(hit.normal);
            return true;
        }

        groundPos = origin;
        groundRot = Quaternion.identity;
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

    private Vector3 ClampHyperextensionSmart(Vector3 targetPos, Transform hip, bool hasValidGround)
    {
        if (hip == null) return targetPos;

        Vector3 hipToTarget = targetPos - hip.position;
        float dist = hipToTarget.magnitude;

        float allowedReach = hasValidGround ? (_maxLegReach * 1.15f) : _maxLegReach;

        if (dist > allowedReach && dist > 0.001f)
        {
            Vector3 clampedDir = hipToTarget / dist;
            float clampedX = hip.position.x + (clampedDir.x * allowedReach);

            if (hasValidGround)
            {
                Vector3 reRayOrigin = new Vector3(clampedX, hip.position.y + 0.5f, targetPos.z);
                RaycastHit2D reHit = Physics2D.Raycast(reRayOrigin, Vector2.down, _raycastDistance + 1f, _groundLayer);

                if (reHit.collider != null && Vector2.Angle(reHit.normal, Vector2.up) <= _maxSlopeAngle)
                {
                    float newGroundY = reHit.point.y + _footYOffset;
                    Vector3 reValidatedPos = new Vector3(clampedX, newGroundY, targetPos.z);

                    if ((reValidatedPos - hip.position).magnitude <= allowedReach * 1.08f)
                    {
                        return reValidatedPos;
                    }
                }
            }

            return hip.position + clampedDir * allowedReach;
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
        if (!Application.isPlaying) return;

        Gizmos.color = (_activeSwingLeg == ActiveLeg.Left) ? Color.magenta : Color.cyan;
        Gizmos.DrawWireSphere(_leftPlantedPos, 0.06f);

        Gizmos.color = (_activeSwingLeg == ActiveLeg.Right) ? Color.magenta : Color.yellow;
        Gizmos.DrawWireSphere(_rightPlantedPos, 0.06f);
    }
}