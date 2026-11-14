using UnityEngine;

public class DynamicArmWeaponController2D : MonoBehaviour
{
    [Header("Referensi Tubuh")]
    [SerializeField] private Transform _shoulderPivot;      // B_Body_Middle (pangkal bahu)
    [SerializeField] private Transform _gfxTransform;       // Objek GFX (yang di-flip sumbu X)
    [SerializeField] private Rigidbody2D _playerRb;

    [Header("Target IK Tangan")]
    [SerializeField] private Transform _rHandTarget;        // R_Hand_Target
    [SerializeField] private Transform _lHandTarget;        // L_Hand_Target
    [SerializeField] private Transform _lHandGripPoint;     // Grip_Point_L (di gagang kapak)

    [Header("Tulang Telapak Tangan")]
    [SerializeField] private Transform _rHandBone;          // B_R_Hand
    [SerializeField] private Transform _lHandBone;          // B_L_Hand
    [SerializeField] private float _handAngleOffset = 0f;

    [Header("Jangkauan Lengan (Dynamic Reach)")]
    [SerializeField] private float _minReach = 0.25f;       // Jarak saat mouse dekat (siku menekuk ke dada)
    [SerializeField] private float _maxReach = 0.65f;       // Jarak maksimal saat lengan menjulur lurus
    [SerializeField] private float _minAimAngle = -60f;     // Batas bidik ke bawah
    [SerializeField] private float _maxAimAngle = 80f;      // Batas bidik ke atas

    [Header("Inersia & Sway Tangan")]
    [SerializeField] private float _aimLagTime = 0.12f;      // Jeda berat saat tangan mengayun mengikuti mouse
    [SerializeField] private float _walkBobAmount = 0.08f;   // Goyangan tangan naik-turun saat lari
    [SerializeField] private float _walkBobSpeed = 9f;

    private Camera _cam;
    private Vector3 _currentHandTargetPos;
    private Vector3 _handVelocity;
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
        float currentReach = Mathf.Clamp(distanceToMouse, _minReach, _maxReach);

        //Hitung sudut bidik dan batasi agar tidak memutar ke belakang punggung
        float rawAngle = Mathf.Atan2(toMouse.y, toMouse.x * facingDir) * Mathf.Rad2Deg;
        float clampedAngle = Mathf.Clamp(rawAngle, _minAimAngle, _maxAimAngle);
        float rad = clampedAngle * Mathf.Rad2Deg;

        //Tentukan posisi ideal tangan kanan di depan bahu
        Vector3 targetOffset = new Vector3(Mathf.Cos(rad) * currentReach * facingDir, Mathf.Sin(rad) * currentReach, 0f);
        Vector3 desiredHandPos = _shoulderPivot.position + targetOffset;

        //Tambahkan Sway & Bobbing saat berjalan/lari
        float hSpeed = _playerRb != null ? _playerRb.linearVelocity.x : 0f;
        if (Mathf.Abs(hSpeed) > 0.1f)
        {
            _bobTimer += Time.deltaTime * _walkBobSpeed;
            float bobY = Mathf.Sin(_bobTimer) * _walkBobAmount;
            float dragX = -Mathf.Sign(hSpeed) * 0.05f; // Tangan sedikit tertarik ke belakang saat lari
            desiredHandPos += new Vector3(dragX, bobY, 0f);
        }

        _currentHandTargetPos = Vector3.SmoothDamp(_currentHandTargetPos, desiredHandPos, ref _handVelocity, _aimLagTime);
        _rHandTarget.position = _currentHandTargetPos;

        float worldAimAngle = clampedAngle * facingDir;
        Quaternion targetHandRotation = Quaternion.Euler(0f, 0f, worldAimAngle + (facingDir * _handAngleOffset));
        if (_rHandBone != null)
            _rHandBone.rotation = targetHandRotation;

        if (_rHandTarget != null & _lHandGripPoint != null)
            _lHandGripPoint.position = _lHandGripPoint.position;
            if (_lHandBone != null)
                 _lHandBone.rotation = targetHandRotation;
    }
}