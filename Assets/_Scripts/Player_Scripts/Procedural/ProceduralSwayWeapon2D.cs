using UnityEngine;

public class ProceduralWeaponSway2D : MonoBehaviour
{
    [Header("Referensi")]
    [SerializeField] private Rigidbody2D _playerRb;
    [SerializeField] private Transform _weaponPivot;

    [Header("Tulang Telapak Tangan (Grip Hand Bones)")]
    [SerializeField] private Transform _rightHandBone; // Masukkan bone: B_R_Hand
    [SerializeField] private Transform _leftHandBone;  // Masukkan bone: B_L_Hand
    [SerializeField] private float _handRotationOffset = 90f; // Penyesuaian sudut telapak

    [Header("Inersia Berat Senjata")]
    [SerializeField] private float _weightLagTime = 0.22f; // Semakin besar, semakin terasa berat dan lambat menyusul mouse

    [Header("Sway Saat Diam (Idle Breath)")]
    [SerializeField] private float _idleSwayAngle = 4f;    // Derajat ayunan nafas
    [SerializeField] private float _idleSwaySpeed = 2f;

    [Header("Sway Saat Bergerak (Walk Sway)")]
    [SerializeField] private float _walkSwayAngle = 14f;   // Derajat ayunan saat lari (dibuat lebih terasa)
    [SerializeField] private float _walkSwaySpeed = 10f;
    [SerializeField] private float _walkDragOffset = 12f;  // Seberapa jauh pedang tertinggal ke belakang saat lari

    private Camera _cam;
    private float _currentAngle;
    private float _angleVelocity;
    private float _swayTimer;
    private Vector3 _initialLocalPos;

    private void Start()
    {
        _cam = Camera.main;
        if (_weaponPivot == null) _weaponPivot = transform;
        _initialLocalPos = _weaponPivot.localPosition;
    }

    private void LateUpdate()
    {
        if (_cam == null) return;

        // 1. Hitung sudut mouse di dunia 2D
        Vector3 mouseWorldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 aimDirection = mouseWorldPos - _weaponPivot.position;
        float baseAimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

        // 2. Hitung Efek Sway (Idle vs Bergerak)
        float horizontalSpeed = _playerRb != null ? _playerRb.linearVelocity.x : 0f;
        float targetAngle = baseAimAngle;

        _swayTimer += Time.deltaTime;

        if (Mathf.Abs(horizontalSpeed) > 0.2f)
        {
            // Saat Jalan/Lari: Kapak berayun kencang dan tertinggal ke belakang arah lari
            float walkSway = Mathf.Sin(_swayTimer * _walkSwaySpeed) * _walkSwayAngle;
            float dragOffset = -Mathf.Sign(horizontalSpeed) * _walkDragOffset;
            targetAngle += (walkSway + dragOffset);
        }
        else
        {
            // Saat Diam: Ayunan nafas naik-turun santai
            float idleSway = Mathf.Sin(_swayTimer * _idleSwaySpeed) * _idleSwayAngle;
            targetAngle += idleSway;
        }

        // 3. [Pasti] Rotasi dengan SmoothDampAngle untuk menciptakan massa berat
        _currentAngle = Mathf.SmoothDampAngle(_currentAngle, targetAngle, ref _angleVelocity, _weightLagTime);
        _weaponPivot.rotation = Quaternion.Euler(0f, 0f, _currentAngle);

        // 4. Balik orientasi sumbu Y sprite agar tidak terbalik saat membidik ke kiri
        HandleWeaponFlipping(baseAimAngle);

        // 5. Kunci rotasi kedua telapak tangan agar sejajar dengan gagang kapak
        AlignHandsWithWeapon();
    }

    private void AlignHandsWithWeapon()
    {
        // Paksa tulang telapak tangan menghadap searah dengan sudut senjata
        Quaternion handRot = _weaponPivot.rotation * Quaternion.Euler(0, 0, _handRotationOffset);
        
        if (_rightHandBone != null) _rightHandBone.rotation = handRot;
        if (_leftHandBone != null)  _leftHandBone.rotation = handRot;
    }

    private void HandleWeaponFlipping(float angle)
    {
        if (Mathf.Abs(angle) > 90f)
        {
            _weaponPivot.localScale = new Vector3(1f, -1f, 1f);
        }
        else
        {
            _weaponPivot.localScale = new Vector3(1f, 1f, 1f);
        }
    }
}