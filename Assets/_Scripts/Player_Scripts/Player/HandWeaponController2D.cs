// using UnityEngine;

// public class HandWeaponController2D : MonoBehaviour
// {
//     [Header("Referensi Badan & Bahu")]
//     [SerializeField] private Transform _shoulderPivot;
//     [SerializeField] private Rigidbody2D _playerRb;
//     [SerializeField] private Transform _gfxTransform;

//     [Header("Target IK Tangan")]
//     [SerializeField] private Transform _dominantHandTarget;  // R_Hand_Target
//     [SerializeField] private Transform _offHandTarget;       // L_Hand_Target

//     [Header("Senjata & Gagang")]
//     [SerializeField] private Transform _weaponPivot;         // Weapon_Pivot
//     [SerializeField] private Transform _offHandGripPoint;    // Grip_Point_L di gagang kapak

//     [Header("Tulang Telapak Tangan")]
//     [SerializeField] private Transform _dominantHandBone;    // B_R_Hand
//     [SerializeField] private Transform _offHandBone;         // B_L_Hand
//     [SerializeField] private float _handAngleOffset = 0f;

//     [Header("Batas Gerak Lengan")]
//     [SerializeField] private float _armReach = 0.5f;
//     [SerializeField] private float _minAimAngle = -45f;
//     [SerializeField] private float _maxAimAngle = 65f;

//     [Header("Inersia Berat Senjata (Sway)")]
//     [SerializeField] private float _weaponWeightLag = 0.2f;
//     [SerializeField] private float _walkSwayAmount = 12f;

//     private Camera _cam;
//     private float _currentWeaponAngle;
//     private float _angleVelocity;
//     private float _swayTimer;

//     private void Start()
//     {
//         _cam = Camera.main;
//     }

//     private void LateUpdate()
//     {
//         if (_cam == null || _shoulderPivot == null || _weaponPivot == null) return;

//         float facingDir = _gfxTransform != null ? Mathf.Sign(_gfxTransform.localScale.x) : 1f;

//         //Hitung sudut bidik dari Bahu ke Mouse
//         Vector3 mouseWorldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
//         Vector2 aimDir = (mouseWorldPos - _shoulderPivot.position);
        
//         // Sudut disesuaikan dengan arah hadap karakter
//         float rawAngle = Mathf.Atan2(aimDir.y, aimDir.x * facingDir) * Mathf.Rad2Deg;
//         float clampedAimAngle = Mathf.Clamp(rawAngle, _minAimAngle, _maxAimAngle);
//         float rad = clampedAimAngle * Mathf.Deg2Rad;

//         // Posisikan Tangan Utama mengorbit di depan bahu
//         Vector3 handOffset = new Vector3(Mathf.Cos(rad) * _armReach * facingDir, Mathf.Sin(rad) * _armReach, 0f);
//         _dominantHandTarget.position = _shoulderPivot.position + handOffset;
//         _weaponPivot.position = _dominantHandTarget.position;

//         // Hitung Rotasi Senjata + Sway Inersia (Kepala kapak yang mengayun)
//         float targetAngle = clampedAimAngle * facingDir;
//         float hSpeed = _playerRb != null ? _playerRb.linearVelocity.x : 0f;

//         if (Mathf.Abs(hSpeed) > 0.1f)
//         {
//             _swayTimer += Time.deltaTime * 10f;
//             float movementSway = Mathf.Sin(_swayTimer) * _walkSwayAmount;
//             float drag = -Mathf.Sign(hSpeed) * 8f;
//             targetAngle += (movementSway + drag);
//         }

//         // Putar senjata secara halus (bobot berat kapak)
//         _currentWeaponAngle = Mathf.SmoothDampAngle(_currentWeaponAngle, targetAngle, ref _angleVelocity, _weaponWeightLag);
//         _weaponPivot.rotation = Quaternion.Euler(0f, 0f, _currentWeaponAngle);

//         // Tangan Kedua (Kiri) mengunci di gagang kapak bagian bawah
//         if (_offHandTarget != null && _offHandGripPoint != null)
//         {
//             _offHandTarget.position = _offHandGripPoint.position;
//         }

//         //  Putar tulang telapak tangan agar mencengkeram gagang
//         Quaternion gripRot = _weaponPivot.rotation * Quaternion.Euler(0, 0, _handAngleOffset);
//         if (_dominantHandBone != null) _dominantHandBone.rotation = gripRot;
//         if (_offHandBone != null) _offHandBone.rotation = gripRot;
//     }
// }