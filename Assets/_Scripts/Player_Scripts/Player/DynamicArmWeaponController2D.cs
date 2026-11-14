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

    [Header("Inersia & Sway")]
    [SerializeField] private float _aimLagTime = 0.15f;      // Kelembutan ayunan kapak berat
    [SerializeField] private float _walkBobAmount = 0.05f;
    [SerializeField] private float _walkBobSpeed = 8f;

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

        // 1. Arah Hadap Karakter
        float facingDir = _gfxTransform != null ? Mathf.Sign(_gfxTransform.localScale.x) : 1f;

        // 2. Baca Posisi Mouse & Hitung Jarak
        Vector3 mouseWorldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        Vector2 toMouse = mouseWorldPos - _shoulderPivot.position;

        // Dynamic Reach: makin dekat mouse, makin dekat target ke bahu (siku menekuk)
        float distance = toMouse.magnitude;
        float currentReach = Mathf.Clamp(distance, _minReach, _maxReach);

        // 3. Sudut Bidik
        float rawAngle = Mathf.Atan2(toMouse.y, toMouse.x * facingDir) * Mathf.Rad2Deg;
        float clampedAngle = Mathf.Clamp(rawAngle, _minAimAngle, _maxAimAngle);

        // 4. Inersia Sudut (Sway Berat Senjata)
        _currentAimAngle = Mathf.SmoothDampAngle(_currentAimAngle, clampedAngle, ref _angleVelocity, _aimLagTime);
        float rad = _currentAimAngle * Mathf.Deg2Rad;

        // 5. Hitung Posisi Target Tangan Kanan di Depan Bahu
        Vector3 targetOffset = new Vector3(Mathf.Cos(rad) * currentReach * facingDir, Mathf.Sin(rad) * currentReach, 0f);
        Vector3 desiredHandPos = _shoulderPivot.position + targetOffset;

        // Bobbing saat jalan
        float hSpeed = _playerRb != null ? _playerRb.linearVelocity.x : 0f;
        if (Mathf.Abs(hSpeed) > 0.1f)
        {
            _bobTimer += Time.deltaTime * _walkBobSpeed;
            float bobY = Mathf.Sin(_bobTimer) * _walkBobAmount;
            desiredHandPos += new Vector3(0f, bobY, 0f);
        }

        // Terapkan Posisi ke R_Hand_Target
        _currentHandTargetPos = Vector3.SmoothDamp(_currentHandTargetPos, desiredHandPos, ref _handVelocity, 0.05f);
        _rHandTarget.position = _currentHandTargetPos;

        // 6. Putar Telapak Tangan Kanan (Kapak ikut berputar karena kapak ada di dalam B_R_Hand)
        float worldAimAngle = _currentAimAngle * facingDir;
        Quaternion gripRotation = Quaternion.Euler(0f, 0f, worldAimAngle + (facingDir * _handAngleOffset));
        
        if (_rHandBone != null)
            _rHandBone.rotation = gripRotation;

        // 7. [KUNCI VIDEO 2]: Kunci Target Tangan Kiri persis di Grip Bawah Gagang
        if (_lHandTarget != null && _lHandGripPoint != null)
        {
            _lHandTarget.position = _lHandGripPoint.position;
            
            if (_lHandBone != null)
                _lHandBone.rotation = gripRotation;
        }
    }
}