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
    [SerializeField] private float _footAlignSpeed = 15f; // <--- PERBAIKAN ERROR CS0103

    // State Tracking
    public LocomotionState CurrentState { get; private set; } = LocomotionState.Idle;
    public float WalkCycleNormalized { get; private set; } // Diekspos untuk sinkronisasi Pelvis Bob

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

        ApplyTargetTransforms();
        _isInitialized = true;
    }

    private void UpdateLocomotionState()
    {
        // PERBAIKAN ERROR CS1061: Murni membaca IsGrounded bawaan PlatformerMovement
        bool isTrulyAirborne = (_movement != null) && !_movement.IsGrounded;

        if (isTrulyAirborne)
        {
            CurrentState = LocomotionState.Airborne;
            return;
        }

        if (CurrentState == LocomotionState.Airborne && _movement.IsGrounded)
        {
            CurrentState = LocomotionState.Landing;
            _stepProgress = 0f;
            return;
        }

        if (CurrentState == LocomotionState.Landing)
        {
            _stepProgress += Time.deltaTime * 12f;
            if (_stepProgress >= 1f)
            {
                CurrentState = LocomotionState.Idle;
                _stepProgress = 0f;
            }
            return;
        }

        float vx = _movement != null ? Mathf.Abs(_movement.PlayerRigidbody.linearVelocity.x) : 0f;
        bool isMoving = vx > 0.12f || (_movement != null && _movement.IsMoving);

        CurrentState = isMoving ? LocomotionState.Walking : LocomotionState.Idle;
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

        SnapFootToGround(true, out Vector3 targetLeft, out Quaternion rotLeft);
        SnapFootToGround(false, out Vector3 targetRight, out Quaternion rotRight);

        _leftPlantedPos = Vector3.Lerp(_leftPlantedPos, targetLeft, Time.deltaTime * 8f);
        _leftPlantedRot = Quaternion.Slerp(_leftPlantedRot, rotLeft, Time.deltaTime * _footAlignSpeed);

        _rightPlantedPos = Vector3.Lerp(_rightPlantedPos, targetRight, Time.deltaTime * 8f);
        _rightPlantedRot = Quaternion.Slerp(_rightPlantedRot, rotRight, Time.deltaTime * _footAlignSpeed);

        WalkCycleNormalized = 0f;
    }

    private void ProcessWalkingState()
    {
        if (_activeSwingLeg != ActiveLeg.None)
        {
            ProgressSwingStep();
            return;
        }

        float vx = _movement.PlayerRigidbody.linearVelocity.x;
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
            {
                TriggerStep(true, moveDir, dynamicStride);
            }
            else if (rightDist > dynamicStride * 0.75f)
            {
                TriggerStep(false, moveDir, dynamicStride);
            }
        }
        else
        {
            if (rightDist > dynamicStride * 0.75f)
            {
                TriggerStep(false, moveDir, dynamicStride);
            }
            else if (leftDist > dynamicStride * 0.75f)
            {
                TriggerStep(true, moveDir, dynamicStride);
            }
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

        if (hit.collider != null && Vector2.Angle(hit.normal, Vector2.up) <= _maxSlopeAngle)
        {
            landingPos = new Vector3(hit.point.x, hit.point.y + _footYOffset, bodyPos.z);
            if (hit.normal.y > 0.3f)
            {
                float angle = Mathf.Atan2(hit.normal.y, hit.normal.x) * Mathf.Rad2Deg - 90f;
                landingRot = Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, -_maxSlopeAngle, _maxSlopeAngle));
            }
        }
        else
        {
            float fallbackY = isLeft ? _rightPlantedPos.y : _leftPlantedPos.y;
            landingPos = new Vector3(predictedX, fallbackY, bodyPos.z);
        }

        Transform hip = isLeft ? _leftHip : _rightHip;
        landingPos = ClampHyperextension(landingPos, hip);

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

        // PERBAIKAN ERROR CS1061 (BARIS 325):
        // Deteksi elevasi tangga otomatis berdasarkan perbedaan tinggi landing target vs start
        float targetY = (_activeSwingLeg == ActiveLeg.Left) ? _leftSwingTarget.y : _rightSwingTarget.y;
        float startY = (_activeSwingLeg == ActiveLeg.Left) ? _leftSwingStart.y : _rightSwingStart.y;
        float elevationDelta = targetY - startY;

        float currentStepHeight = _stepHeight;
        if (elevationDelta > 0.08f)
        {
            // Jika melangkah naik tangga/undakan, naikkan lengkungan ayunan secara adaptif
            currentStepHeight += Mathf.Clamp(elevationDelta * 0.5f, 0f, 0.2f);
        }

        float arcY = Mathf.Sin(t * Mathf.PI) * currentStepHeight;

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

        Vector3 targetLeft = _leftHome != null ? _leftHome.position : transform.position;
        Vector3 targetRight = _rightHome != null ? _rightHome.position : transform.position;

        float vy = _movement != null ? _movement.PlayerRigidbody.linearVelocity.y : 0f;
        if (vy < -2.5f)
        {
            float legDrop = Mathf.Clamp(-vy * 0.03f, 0f, 0.2f);
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

        _leftPlantedPos = Vector3.Lerp(_leftPlantedPos, targetLeft, Time.deltaTime * 18f);
        _rightPlantedPos = Vector3.Lerp(_rightPlantedPos, targetRight, Time.deltaTime * 18f);

        _leftPlantedRot = Quaternion.Slerp(_leftPlantedRot, rotLeft, Time.deltaTime * 18f);
        _rightPlantedRot = Quaternion.Slerp(_rightPlantedRot, rotRight, Time.deltaTime * 18f);
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
            point = ClampHyperextension(point, hip);

            groundPos = point;

            float angle = Mathf.Atan2(hit.normal.y, hit.normal.x) * Mathf.Rad2Deg - 90f;
            groundRot = Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, -_maxSlopeAngle, _maxSlopeAngle));
            return true;
        }

        groundPos = origin;
        groundRot = Quaternion.identity;
        return false;
    }

    private Vector3 ClampHyperextension(Vector3 targetPos, Transform hip)
    {
        if (hip == null) return targetPos;

        Vector3 hipToTarget = targetPos - hip.position;
        float dist = hipToTarget.magnitude;

        if (dist > _maxLegReach && dist > 0.001f)
        {
            return hip.position + (hipToTarget / dist) * _maxLegReach;
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