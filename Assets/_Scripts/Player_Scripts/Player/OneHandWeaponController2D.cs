using UnityEngine;

public class OneHandWeaponController2D : MonoBehaviour
{
    [Header("Referensi Tubuh")]
    [SerializeField] private Transform _shoulderPivot;     
    [SerializeField] private Transform _gfxTransform;      
    [SerializeField] private Rigidbody2D _playerRb;

    [Header("Target & Tulang Tangan Utama")]
    [SerializeField] private Transform _rHandTarget;     
    [SerializeField] private Transform _rHandBone;        
    [SerializeField] private float _handAngleOffset = -15f;
    [Header("Tangan Kiri (Pose Penyeimbang / Santai)")]
    [SerializeField] private Transform _leftShoulderPivot;
    [SerializeField] private Transform _lHandTarget;
    [SerializeField] private Vector2 _lHandIdleOffset = new Vector2(-0.05f, -0.38f); 

    [Header("Jangkauan Lengan (Dynamic Reach)")]
    [SerializeField] private float _minReach = 0.20f;      
    [SerializeField] private float _maxReach = 0.58f;     
    [SerializeField] private float _minAimAngle = -55f;    
    [SerializeField] private float _maxAimAngle = 70f;      

    [Header("Jarak Mouse di Layar (Remapping)")]
    [SerializeField] private float _mouseNearDist = 0.8f;  
    [SerializeField] private float _mouseFarDist = 2.2f;  

    [Header("Sistem Pegas Lengan (Spring Dynamics)")]
    [SerializeField] private float _springStiffness = 130f;
    [SerializeField] private float _springDamping = 14f;    

    [Header("Inersia & Sway Senjata")]
    [SerializeField] private float _aimLagTime = 0.12f; 
    [SerializeField] private float _walkBobAmount = 0.06f;
    [SerializeField] private float _walkBobSpeed = 8f;

    private float _currentSpringReach = 0.35f;
    private float _reachVelocity;
    private Camera _cam;
    private Vector3 _currentHandTargetPos;
    private Vector3 _handVelocity;
    private float _currentAimAngle;
    private float _angleVelocity;
    private float _bobTimer;

    private void Start()
    {
        _cam = Camera.main;
        if (_rHandTarget != null) _currentHandTargetPos = _rHandTarget.position;
    }

    private void LateUpdate()
    {
        if (_cam == null || _shoulderPivot == null || _rHandTarget == null) return;

        float facingDir = _gfxTransform != null ? Mathf.Sign(_gfxTransform.localScale.x) : 1f;

        Vector3 mouseWorldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        Vector2 toMouse = mouseWorldPos - _shoulderPivot.position;

        float distanceToMouse = toMouse.magnitude;
        float reachPercent = Mathf.InverseLerp(_mouseNearDist, _mouseFarDist, distanceToMouse);
        float desiredReach = Mathf.Lerp(_minReach, _maxReach, reachPercent);

        // Simulasi Fisika Pegas Maju-Mundur
        float springForce = (desiredReach - _currentSpringReach) * _springStiffness;
        float dampingForce = _reachVelocity * _springDamping;
        float acceleration = springForce - dampingForce;

        _reachVelocity += acceleration * Time.deltaTime;
        _currentSpringReach += _reachVelocity * Time.deltaTime;

        float rawAngle = Mathf.Atan2(toMouse.y, toMouse.x * facingDir) * Mathf.Rad2Deg;
        float clampedAngle = Mathf.Clamp(rawAngle, _minAimAngle, _maxAimAngle);

        _currentAimAngle = Mathf.SmoothDampAngle(_currentAimAngle, clampedAngle, ref _angleVelocity, _aimLagTime);
        float rad = _currentAimAngle * Mathf.Deg2Rad;

        Vector3 targetOffset = new Vector3(Mathf.Cos(rad) * _currentSpringReach * facingDir, Mathf.Sin(rad) * _currentSpringReach, 0f);
        Vector3 desiredHandPos = _shoulderPivot.position + targetOffset;

        // Tambahan Bobbing saat lari
        float hSpeed = _playerRb != null ? _playerRb.linearVelocity.x : 0f;
        if (Mathf.Abs(hSpeed) > 0.1f)
        {
            _bobTimer += Time.deltaTime * _walkBobSpeed;
            float bobY = Mathf.Sin(_bobTimer) * _walkBobAmount;
            desiredHandPos += new Vector3(0f, bobY, 0f);
        }

        _currentHandTargetPos = Vector3.SmoothDamp(_currentHandTargetPos, desiredHandPos, ref _handVelocity, 0.04f);
        _rHandTarget.position = _currentHandTargetPos;

        //  Rotasi Telapak Tangan & Pedang
        float straightPercent = Mathf.InverseLerp(_minReach, _maxReach, _currentSpringReach);
        float effectiveOffset = Mathf.Lerp(_handAngleOffset, _handAngleOffset * 0.2f, straightPercent);

        float worldAimAngle = _currentAimAngle * facingDir;
        Quaternion swordRotation = Quaternion.Euler(0f, 0f, worldAimAngle + (facingDir * effectiveOffset));

        if (_rHandBone != null)
            _rHandBone.rotation = swordRotation;

        //  Pose Santai Tangan Kiri (Menempel santai di samping dada)
        if (_lHandTarget != null)
        {
            Transform leftPivot = _leftShoulderPivot != null ? _leftShoulderPivot : _shoulderPivot;

            Vector3 leftHandRestPos = leftPivot.position + new Vector3(_lHandIdleOffset.x * facingDir, _lHandIdleOffset.y, 0f);
            _lHandTarget.position = Vector3.Lerp(_lHandTarget.position, leftHandRestPos, Time.deltaTime * 10f);
        }
    }

    private void OnDrawGizmos()
    {
        if (_shoulderPivot != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_shoulderPivot.position, _minReach);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_shoulderPivot.position, _maxReach);

            if (_rHandTarget != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_shoulderPivot.position, _rHandTarget.position);
                Gizmos.DrawSphere(_rHandTarget.position, 0.04f);
            }
        }
    }
}