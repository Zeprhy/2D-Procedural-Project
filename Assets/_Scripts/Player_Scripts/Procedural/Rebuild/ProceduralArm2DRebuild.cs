using UnityEngine;

/// <summary>
/// Runtime procedural arm controller for 2D characters.
/// Handles reach spring, aim lag, two-bone IK, shoulder offset, recoil, and idle/rest poses.
/// Designed to be driven by RightHandController2D / OffHandController2D via simple API calls.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Procedural Animation/Procedural Arm 2D Rebuild")]
public class ProceduralArm2DRebuild : MonoBehaviour
{
    #region Inspector References

    [Header("References")]
    [SerializeField, Tooltip("Shoulder pivot transform (root of arm hierarchy)")]
    private Transform _shoulderPivot;
    
    [SerializeField, Tooltip("IK target transform (drives hand sprite / weapon)")]
    private Transform _handTarget;
    
    [SerializeField, Tooltip("Visual hand bone transform (receives rotation)")]
    private Transform _handBone;
    
    [SerializeField, Tooltip("Graphics transform for facing direction (scale.x sign)")]
    private Transform _gfxTransform;

    [Header("Optional: Elbow Hint for Two-Bone IK")]
    [SerializeField, Tooltip("Optional transform used as pole vector for elbow bend direction")]
    private Transform _elbowHint;

    #endregion

    #region Inspector Config - Reach Spring

    [Header("Reach Spring")]
    [SerializeField, Min(0f)] private float _minReach = 0.20f;
    [SerializeField, Min(0f)] private float _maxReach = 0.58f;
    [SerializeField, Min(0f)] private float _springStiffness = 130f;
    [SerializeField, Min(0f)] private float _springDamping = 14f;
    [SerializeField, Min(0f)] private float _mouseNearDist = 0.8f;
    [SerializeField, Min(0f)] private float _mouseFarDist = 2.2f;

    #endregion

    #region Inspector Config - Aim Lag

    [Header("Aim Lag")]
    [SerializeField] private float _minAimAngle = -55f;
    [SerializeField] private float _maxAimAngle = 70f;
    [SerializeField, Min(0f)] private float _aimLagTime = 0.12f;

    #endregion

    #region Inspector Config - Pose

    [Header("Pose")]
    [SerializeField] private float _handAngleOffset = -20f;
    [SerializeField] private Vector2 _idleOffset = new(0.12f, -0.35f);
    [SerializeField] private float _idleAngle = -65f;

    #endregion

    #region Inspector Config - Two-Bone IK (Optional)

    [Header("Two-Bone IK (Optional)")]
    [SerializeField] private bool _useTwoBoneIK = false;
    [SerializeField, Min(0.01f)] private float _upperArmLength = 0.30f;
    [SerializeField, Min(0.01f)] private float _forearmLength = 0.35f;

    #endregion

    #region Inspector Config - Shoulder Offset

    [Header("Shoulder Auto-Offset")]
    [SerializeField] private bool _useShoulderOffset = true;
    [SerializeField, Min(0f)] private float _maxShoulderUpOffset = 0.15f;
    [SerializeField, Min(0f)] private float _maxShoulderDownOffset = 0.10f;
    [SerializeField] private float _shoulderUpThreshold = 30f;
    [SerializeField] private float _shoulderDownThreshold = -45f;
    [SerializeField, Min(0f)] private float _shoulderOffsetSpeed = 8f;

    #endregion

    #region Inspector Config - Recoil

    [Header("Recoil")]
    [SerializeField, Min(0f)] private float _recoilRecoverySpeed = 120f;
    [SerializeField, Min(0f)] private float _maxRecoilAngle = 15f;

    #endregion

    #region Runtime State

    private float _currentReach;
    private float _reachVelocity;
    private float _currentAimAngle;
    private float _aimAngleVelocity;
    private Vector3 _currentHandTargetPos;
    private Vector3 _handVelocity;
    private float _currentShoulderOffset;
    private float _currentRecoil;
    private bool _isAiming;
    private Vector2 _lastAimTarget;

    // Config structs (built from inspector values)
    private ProceduralArmMath2DRebuild.ReachSpringConfig _reachConfig;
    private ProceduralArmMath2DRebuild.AimLagConfig _aimConfig;
    private ProceduralArmMath2DRebuild.TwoBoneIKConfig _ikConfig;
    private ProceduralArmMath2DRebuild.ShoulderOffsetConfig _shoulderConfig;
    private ProceduralArmMath2DRebuild.RecoilConfig _recoilConfig;

    #endregion

    #region Public Properties

    /// <summary>Current facing direction (1 = right, -1 = left).</summary>
    public float FacingDirection
    {
        get
        {
            if (_gfxTransform == null || Mathf.Abs(_gfxTransform.localScale.x) < 0.0001f)
                return 1f;
            return Mathf.Sign(_gfxTransform.localScale.x);
        }
    }

    /// <summary>Current aim direction in world space (normalized).</summary>
    public Vector2 AimDirection
    {
        get
        {
            if (!_isAiming || _shoulderPivot == null) return Vector2.right * FacingDirection;
            Vector2 dir = _lastAimTarget - (Vector2)_shoulderPivot.position;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right * FacingDirection;
        }
    }

    /// <summary>Current hand target world position.</summary>
    public Vector3 HandTargetPosition => _handTarget != null ? _handTarget.position : _currentHandTargetPos;

    /// <summary>Whether the arm is currently aiming (not in idle/rest pose).</summary>
    public bool IsAiming => _isAiming;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        BuildConfigs();
        CacheInitialState();
    }

    private void OnValidate()
    {
        BuildConfigs();
    }

    private void LateUpdate()
    {
        if (_shoulderPivot == null || _handTarget == null) return;

        float facingDir = FacingDirection;
        float dt = Time.deltaTime;

        if (_isAiming)
        {
            UpdateAiming(facingDir, dt);
        }
        else
        {
            UpdateIdle(facingDir, dt);
        }

        // Apply shoulder offset to shoulder pivot (if enabled)
        if (_useShoulderOffset && _shoulderPivot != null)
        {
            Vector3 localPos = _shoulderPivot.localPosition;
            localPos.y = _currentShoulderOffset;
            _shoulderPivot.localPosition = localPos;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Sets the aim target for this frame. Call every frame from controller (e.g., RightHandController2D).
    /// </summary>
    /// <param name="mouseWorldPos">Mouse position in world space.</param>
    /// <param name="isAiming">Whether the player is currently aiming (holding aim button).</param>
    public void SetAimTarget(Vector2 mouseWorldPos, bool isAiming)
    {
        _lastAimTarget = mouseWorldPos;
        _isAiming = isAiming;
    }

    /// <summary>
    /// Adds recoil impulse (degrees) to the arm. Call when weapon fires.
    /// </summary>
    /// <param name="recoilDegrees">Recoil impulse in degrees (positive = upward kick).</param>
    public void AddRecoil(float recoilDegrees)
    {
        _currentRecoil = ProceduralArmMath2DRebuild.UpdateRecoil(_currentRecoil, recoilDegrees, _recoilConfig, 0f); // immediate add
    }

    /// <summary>
    /// Updates configuration from a weapon (optional). Called when weapon is equipped.
    /// </summary>
    public void ApplyWeaponConfig(WeaponLogic weapon)
    {
        if (weapon == null) return;
        
        BaseWeaponSO data = weapon.WeaponData;
        if (data == null || !data.OverrideArmConfig) return;

        // Override arm config from weapon
        _minReach = data.MinReach;
        _maxReach = data.MaxReach;
        _springStiffness = data.SpringStiffness;
        _springDamping = data.SpringDamping;
        _minAimAngle = data.MinAimAngle;
        _maxAimAngle = data.MaxAimAngle;
        _aimLagTime = data.AimLagTime;
        _handAngleOffset = data.HandAngleOffset;
        _idleOffset = data.IdleOffset;
        _idleAngle = data.IdleAngle;

        BuildConfigs();
    }

    /// <summary>
    /// Resets arm to default config (call when unequipping weapon).
    /// </summary>
    public void ResetToDefaultConfig()
    {
        // Values already serialized in inspector, just rebuild configs
        BuildConfigs();
    }

    /// <summary>
    /// Instantly snaps arm to aim target (no smoothing). Useful for spawn/teleport.
    /// </summary>
    public void SnapToAimTarget(Vector2 mouseWorldPos)
    {
        if (_shoulderPivot == null || _handTarget == null) return;

        float facingDir = FacingDirection;
        Vector2 toMouse = mouseWorldPos - (Vector2)_shoulderPivot.position;
        float dist = toMouse.magnitude;

        _currentReach = ProceduralArmMath2DRebuild.UpdateSpringReach(dist, _reachConfig, ref _reachVelocity, _currentReach, 0f);
        _currentAimAngle = ProceduralArmMath2DRebuild.UpdateAimAngle(toMouse, facingDir, _aimConfig, ref _aimAngleVelocity, _currentAimAngle);

        Vector3 targetPos = ProceduralArmMath2DRebuild.GetHandWorldPos(_shoulderPivot.position, _currentReach, _currentAimAngle, facingDir);
        _currentHandTargetPos = targetPos;
        _handTarget.position = targetPos;

        if (_handBone != null)
        {
            Quaternion rot = ProceduralArmMath2DRebuild.GetHandWorldRotation(_currentAimAngle, facingDir, _handAngleOffset);
            _handBone.rotation = rot;
        }

        _isAiming = true;
        _lastAimTarget = mouseWorldPos;
    }

    #endregion

    #region Internal Update Logic

    private void UpdateAiming(float facingDir, float dt)
    {
        Vector2 toMouse = _lastAimTarget - (Vector2)_shoulderPivot.position;
        float dist = toMouse.magnitude;

        // 1. Update reach spring
        _currentReach = ProceduralArmMath2DRebuild.UpdateSpringReach(dist, _reachConfig, ref _reachVelocity, _currentReach, dt);

        // 2. Update aim angle with lag
        _currentAimAngle = ProceduralArmMath2DRebuild.UpdateAimAngle(toMouse, facingDir, _aimConfig, ref _aimAngleVelocity, _currentAimAngle);

        // 3. Update recoil recovery
        _currentRecoil = ProceduralArmMath2DRebuild.UpdateRecoil(_currentRecoil, 0f, _recoilConfig, dt);

        // 4. Compute final aim angle with recoil
        float finalAimAngle = _currentAimAngle + _currentRecoil;

        // 5. Compute hand world position
        Vector3 desiredHandPos = ProceduralArmMath2DRebuild.GetHandWorldPos(_shoulderPivot.position, _currentReach, finalAimAngle, facingDir);

        // 6. Smooth hand target position (small damp for visual polish)
        _currentHandTargetPos = Vector3.SmoothDamp(_currentHandTargetPos, desiredHandPos, ref _handVelocity, 0.04f);
        _handTarget.position = _currentHandTargetPos;

        // 7. Update hand bone rotation
        if (_handBone != null)
        {
            float straightPercent = Mathf.InverseLerp(_minReach, _maxReach, _currentReach);
            float effectiveOffset = Mathf.Lerp(_handAngleOffset, _handAngleOffset * 0.2f, straightPercent);
            Quaternion rot = ProceduralArmMath2DRebuild.GetHandWorldRotation(finalAimAngle, facingDir, effectiveOffset);
            _handBone.rotation = rot;
        }

        // 8. Update shoulder offset
        if (_useShoulderOffset)
        {
            _currentShoulderOffset = ProceduralArmMath2DRebuild.UpdateShoulderOffset(_currentAimAngle, _shoulderConfig, ref _currentShoulderOffset, dt);
        }

        // 9. Two-Bone IK (optional, drives elbow hint if assigned)
        if (_useTwoBoneIK && _elbowHint != null)
        {
            var (elbowPos, _, _, _) = ProceduralArmMath2DRebuild.SolveTwoBoneIK(_shoulderPivot.position, _currentHandTargetPos, _ikConfig);
            _elbowHint.position = elbowPos;
        }
    }

    private void UpdateIdle(float facingDir, float dt)
    {
        // Smooth to idle position
        Vector3 idlePos = ProceduralArmMath2DRebuild.GetIdleHandPos(_shoulderPivot.position, _idleOffset, facingDir);
        _currentHandTargetPos = Vector3.Lerp(_currentHandTargetPos, idlePos, dt * 10f);
        _handTarget.position = _currentHandTargetPos;

        // Smooth to idle rotation
        if (_handBone != null)
        {
            Quaternion idleRot = ProceduralArmMath2DRebuild.GetIdleHandRotation(_idleAngle, facingDir);
            Quaternion smoothed = Quaternion.Slerp(_handBone.rotation, idleRot, dt * 10f);

            // Recoil ditambahkan langsung (tidak ikut di-Slerp) supaya twitch tetap terlihat saat idle.
            _handBone.rotation = smoothed * Quaternion.Euler(0f, 0f, _currentRecoil * facingDir);
        }

        // Reset reach to minimum
        _currentReach = Mathf.MoveTowards(_currentReach, _minReach, dt * 5f);

        // Recover shoulder offset
        if (_useShoulderOffset)
        {
            _currentShoulderOffset = Mathf.MoveTowards(_currentShoulderOffset, 0f, _shoulderOffsetSpeed * dt);
        }

        // Recover recoil
        _currentRecoil = ProceduralArmMath2DRebuild.UpdateRecoil(_currentRecoil, 0f, _recoilConfig, dt);
    }

    #endregion

    #region Helpers

    private void BuildConfigs()
    {
        _reachConfig = new ProceduralArmMath2DRebuild.ReachSpringConfig(_minReach, _maxReach, _mouseNearDist, _mouseFarDist, _springStiffness, _springDamping);
        _aimConfig = new ProceduralArmMath2DRebuild.AimLagConfig(_minAimAngle, _maxAimAngle, _aimLagTime);
        _ikConfig = new ProceduralArmMath2DRebuild.TwoBoneIKConfig(_upperArmLength, _forearmLength, _elbowHint != null, _elbowHint != null ? _elbowHint.position : default);
        _shoulderConfig = new ProceduralArmMath2DRebuild.ShoulderOffsetConfig(_maxShoulderUpOffset, _maxShoulderDownOffset, _shoulderUpThreshold, _shoulderDownThreshold, _shoulderOffsetSpeed);
        _recoilConfig = new ProceduralArmMath2DRebuild.RecoilConfig(_recoilRecoverySpeed, _maxRecoilAngle);
    }

    private void CacheInitialState()
    {
        _currentReach = _minReach;
        _reachVelocity = 0f;
        _currentAimAngle = 0f;
        _aimAngleVelocity = 0f;
        _currentShoulderOffset = 0f;
        _currentRecoil = 0f;

        if (_handTarget != null)
            _currentHandTargetPos = _handTarget.position;
        else if (_shoulderPivot != null)
            _currentHandTargetPos = _shoulderPivot.position;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (_shoulderPivot == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_shoulderPivot.position, 0.05f);

        if (_handTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(_shoulderPivot.position, _handTarget.position);
            Gizmos.DrawWireSphere(_handTarget.position, 0.04f);
        }

        if (_useTwoBoneIK && _elbowHint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_shoulderPivot.position, _elbowHint.position);
            Gizmos.DrawLine(_elbowHint.position, _handTarget != null ? _handTarget.position : _shoulderPivot.position);
            Gizmos.DrawWireSphere(_elbowHint.position, 0.03f);
        }

        // Draw reach range
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(_shoulderPivot.position, _maxReach);
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(_shoulderPivot.position, _minReach);
    }

    #endregion
}