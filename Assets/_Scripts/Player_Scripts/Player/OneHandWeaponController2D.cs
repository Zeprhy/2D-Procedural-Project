using UnityEngine;

public class OneHandWeaponController2D : MonoBehaviour
{
    [Header("Referensi Input")]
    [SerializeField] private PlayerAimTracker2D _aimTracker;

    [Header("Referensi Tubuh")]
    [SerializeField] private Transform _rightShoulderPivot;
    [SerializeField] private Transform _gfxTransform;

    [Header("Target & Tulang Tangan")]
    [SerializeField] private Transform _rHandTarget;
    [SerializeField] private Transform _rHandBone;
    [SerializeField] private float _handAngleOffset = -20f;

    [Header("Senjata yang Sedang Dipegang")]
    [SerializeField] private WeaponLogic _equippedWeapon;

    [Header("Pose Santai Pedang (Saat Klik Kiri Dilepas)")]
    [SerializeField] private Vector2 _swordRestOffset = new Vector2(0.12f, -0.35f);
    [SerializeField] private float _swordRestAngle = -65f;
    [Header("Jangkauan Lengan Saat Membidik")]
    [SerializeField] private float _minReach = 0.20f;
    [SerializeField] private float _maxReach = 0.58f;
    [SerializeField] private float _minAimAngle = -55f;
    [SerializeField] private float _maxAimAngle = 70f;
    [SerializeField] private float _mouseNearDist = 0.8f;
    [SerializeField] private float _mouseFarDist = 2.2f;

    [Header("Fisika Pegas & Inersia")]
    [SerializeField] private float _springStiffness = 130f;
    [SerializeField] private float _springDamping = 14f;
    [SerializeField] private float _aimLagTime = 0.12f;

    private float _currentSpringReach = 0.35f;
    private float _reachVelocity;
    private Vector3 _currentHandTargetPos;
    private Vector3 _handVelocity;
    private float _currentAimAngle;
    private float _angleVelocity;

    private void Start()
    {
        if (_aimTracker == null) _aimTracker = GetComponent<PlayerAimTracker2D>();
        if (_rHandTarget != null) _currentHandTargetPos = _rHandTarget.position;
    }

    private void LateUpdate()
    {
        if (_aimTracker == null || _rightShoulderPivot == null || _rHandTarget == null) return;

        float facingDir = _gfxTransform != null ? Mathf.Sign(_gfxTransform.localScale.x) : 1f;

        if (_aimTracker.IsAimingRight)
        {
            if (_equippedWeapon != null)
                _equippedWeapon.TriggerAttack(gameObject);

            Vector2 toMouse = _aimTracker.MouseWorldPosition - _rightShoulderPivot.position;
            float distanceToMouse = toMouse.magnitude;

            //Hitung Jarak Pegas:
            _currentSpringReach = ProceduralArmMath.UpdateSpringReach(
                distanceToMouse, _mouseNearDist, _mouseFarDist, _minReach, _maxReach, 
                _springStiffness, _springDamping, ref _reachVelocity, _currentSpringReach, Time.deltaTime);

            //Hitung Sudut Bidik:
            _currentAimAngle = ProceduralArmMath.UpdateAimAngle(
                toMouse, facingDir, _minAimAngle, _maxAimAngle, _aimLagTime, ref _angleVelocity, _currentAimAngle);

            float rad = _currentAimAngle * Mathf.Deg2Rad;
            
            // Posisi Target
            Vector3 targetOffset = new Vector3(Mathf.Cos(rad) * _currentSpringReach * facingDir, Mathf.Sin(rad) * _currentSpringReach, 0f);
            Vector3 desiredHandPos = _rightShoulderPivot.position + targetOffset;

            _currentHandTargetPos = Vector3.SmoothDamp(_currentHandTargetPos, desiredHandPos, ref _handVelocity, 0.04f);
            _rHandTarget.position = _currentHandTargetPos;

            // Rotasi Pedang Menghadap Target
            float straightPercent = Mathf.InverseLerp(_minReach, _maxReach, _currentSpringReach);
            float effectiveOffset = Mathf.Lerp(_handAngleOffset, _handAngleOffset * 0.2f, straightPercent);
            float worldAimAngle = _currentAimAngle * facingDir;
            Quaternion swordRotation = Quaternion.Euler(0f, 0f, worldAimAngle + (facingDir * effectiveOffset));

            if (_rHandBone != null) _rHandBone.rotation = swordRotation;
        }

        else
        {
            // Tangan kanan dan pedang turun santai di samping paha
            Vector3 restPos = _rightShoulderPivot.position + new Vector3(_swordRestOffset.x * facingDir, _swordRestOffset.y, 0f);
            _currentHandTargetPos = Vector3.Lerp(_currentHandTargetPos, restPos, Time.deltaTime * 8f);
            _rHandTarget.position = _currentHandTargetPos;

            // Sudut pedang miring santai mengarah ke bawah tanah
            Quaternion restRotation = Quaternion.Euler(0f, 0f, _swordRestAngle * facingDir);
            if (_rHandBone != null) _rHandBone.rotation = Quaternion.Slerp(_rHandBone.rotation, restRotation, Time.deltaTime * 8f);

            _currentSpringReach = _minReach;
        }
    }
}