using UnityEngine;

public class DynamicArmWeaponController2D : MonoBehaviour
{
    [Header("Referensi Tubuh")]
    [SerializeField] private Transform _shoulderPivot;      // Masukkan: B_R_Hand_Main (bahu kanan)
    [SerializeField] private Transform _gfxTransform;       // Masukkan: GFX
    [SerializeField] private Rigidbody2D _playerRb;

    [Header("Target IK Tangan")]
    [SerializeField] private Transform _rHandTarget;        // Masukkan: R_Hand_Target
    [SerializeField] private Transform _lHandTarget;        // Masukkan: L_Hand_Target
    [SerializeField] private Transform _lHandGripPoint;     // Masukkan: Grip_Point_L (di gagang kapak)

    [Header("Tulang Telapak Tangan")]
    [SerializeField] private Transform _rHandBone;          // Masukkan: B_R_Hand
    [SerializeField] private Transform _lHandBone;          // Masukkan: B_L_Hand
    [SerializeField] private float _handAngleOffset = 0f;   // Geser slider ini untuk meluruskan pegangan

    [Header("Jangkauan Lengan (Dynamic Reach)")]
    [SerializeField] private float _minReach = 0.25f;       // Jarak saat mouse dekat (siku menekuk persis video keduamu)
    [SerializeField] private float _maxReach = 0.48f;       // Jarak maksimal lengan menjulur
    [SerializeField] private float _minAimAngle = -50f;     // Batas bidik ke bawah
    [SerializeField] private float _maxAimAngle = 65f;      // Batas bidik ke atas

    [Header("Jarak Mouse di Layar (Remapping)")]
    [SerializeField] private float _mouseNearDist = 0.8f;  
    [SerializeField] private float _mouseFarDist = 3.0f; 

    [Header("Sistem Pegas Lengan (Spring Dynamics)")]
    [SerializeField] private float _springStiffness = 120f;
    [SerializeField] private float _springDamping = 12f;    

    [Header("Inersia & Sway")]
    [SerializeField] private float _aimLagTime = 0.15f;    
    [SerializeField] private float _walkBobAmount = 0.05f;
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

        // Dynamic Reach: makin dekat mouse, makin dekat target ke bahu (siku menekuk
        float distanceToMouse = toMouse.magnitude;
        float reachPercent = Mathf.InverseLerp(_mouseNearDist, _mouseFarDist, distanceToMouse);
        float desiredReach  = Mathf.Lerp(_minReach, _maxReach, reachPercent);

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

        float hSpeed = _playerRb != null ? _playerRb.linearVelocity.x : 0f;
        if (Mathf.Abs(hSpeed) > 0.1f)
        {
            _bobTimer += Time.deltaTime * _walkBobSpeed;
            float bobY = Mathf.Sin(_bobTimer) * _walkBobAmount;
            desiredHandPos += new Vector3(0f, bobY, 0f);
        }

        _currentHandTargetPos = Vector3.SmoothDamp(_currentHandTargetPos, desiredHandPos, ref _handVelocity, 0.05f);
        _rHandTarget.position = _currentHandTargetPos;

        float straightPercent = Mathf.InverseLerp(_minReach, _maxReach, _currentSpringReach);
        float effectiveOffset = Mathf.Lerp(_handAngleOffset, _handAngleOffset * 0.2f, straightPercent);

        float worldAimAngle = _currentAimAngle * facingDir;
        Quaternion gripRotation = Quaternion.Euler(0f, 0f, worldAimAngle + (facingDir * effectiveOffset));

        if (_rHandBone != null)
            _rHandBone.rotation = gripRotation;

        if (_lHandTarget != null && _lHandGripPoint != null)
        {
            _lHandTarget.position = _lHandGripPoint.position;

            if (_lHandBone != null)
                _lHandBone.rotation = gripRotation;
        }
        
        // float worldAimAngle = _currentAimAngle * facingDir;
        // Quaternion gripRotation = Quaternion.Euler(0f, 0f, worldAimAngle + (facingDir * _handAngleOffset));
        
        // if (_rHandBone != null)
        //     _rHandBone.rotation = gripRotation;
        // if (_lHandTarget != null && _lHandGripPoint != null)
        // {
        //     _lHandTarget.position = _lHandGripPoint.position;
            
        //     if (_lHandBone != null)
        //         _lHandBone.rotation = gripRotation;
        // }
    }

    private void OnDrawGizmos()
    {
        if (_shoulderPivot != null)
        {
            // Lingkaran Hijau = Jangkauan Tangan Menekuk (Min Reach)
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_shoulderPivot.position, _minReach);

            // Lingkaran Merah = Jangkauan Tangan Lurus (Max Reach)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_shoulderPivot.position, _maxReach);

            // Garis Kuning = Posisi Target Tangan Saat Ini
            if (_rHandTarget != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_shoulderPivot.position, _rHandTarget.position);
                Gizmos.DrawSphere(_rHandTarget.position, 0.04f);
            }
        }
    }
}