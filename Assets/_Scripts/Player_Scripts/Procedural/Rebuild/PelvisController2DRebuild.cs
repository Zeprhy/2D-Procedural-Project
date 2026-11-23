
using UnityEngine;

[DisallowMultipleComponent]
public class PelvisController2DRebuild : MonoBehaviour
{
    [Header("Referensi Karakter")]
    [Tooltip("Player Root yang memiliki Rigidbody2D")]
    [SerializeField] private Transform _playerRoot;

    [SerializeField] private PlatformerMovement _movement;
    [SerializeField] private ProceduralBipedLeg2DRebuild _legController;

    [SerializeField] private Transform _facingReference;

    [Header("Local Position")]
    [Tooltip("Batas maksimum offset animasi pelvis dari posisi awal")]
    [SerializeField] private float _maxPelvisDeviation = 0.12f;

    [Tooltip("Kecepatan transisi posisi pelvis")]
    [SerializeField] private float _smoothFollowSpeed = 12f;

    [Header("Walk Bobbing")]
    [SerializeField] private float _walkBobAmount = 0.06f;

    [Header("Idle Breathing")]
    [SerializeField] private float _idleBreathingAmount = 0.025f;

    [SerializeField] private float _breathingFrequency = 2.5f;

    [Header("Body Tilt & Inersia")]
    [SerializeField] private float _maxTiltAngle = 6f;

    [SerializeField] private float _tiltSensitivity = 0.8f;

    [SerializeField] private float _tiltRecoverySpeed = 10f;

    // Initial pose
    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;

    // Runtime animation
    private float _currentVerticalOffset;
    private float _currentTiltAngle;
    private float _lastVelocityX;

    private void Awake()
    {
        // Simpan posisi dan rotasi asli B_Body_Main.
        _initialLocalPosition = transform.localPosition;
        _initialLocalRotation = transform.localRotation;

        // Ambil referensi Player Root.
        if (_playerRoot == null)
            _playerRoot = transform.root;

        if (_movement == null && _playerRoot != null)
            _movement = _playerRoot.GetComponent<PlatformerMovement>();

        if (_legController == null && _playerRoot != null)
        {
            _legController =
                _playerRoot.GetComponent<ProceduralBipedLeg2DRebuild>();
        }

        if (_facingReference == null)
            _facingReference = _playerRoot;

        // Hindari lonjakan akselerasi saat pertama kali dijalankan.
        if (_movement != null && _movement.PlayerRigidbody != null)
        {
            _lastVelocityX =
                _movement.PlayerRigidbody.linearVelocity.x;
        }
    }

    private void LateUpdate()
    {
        if (_playerRoot == null || _movement == null)
            return;

        UpdatePelvisPosition();
        UpdatePelvisRotation();
    }

    private void UpdatePelvisPosition()
    {
        float targetOffset = 0f;

        bool isGrounded = _movement.IsGrounded;

        if (isGrounded)
        {
            float velocityX = 0f;

            if (_movement.PlayerRigidbody != null)
            {
                velocityX = Mathf.Abs(
                    _movement.PlayerRigidbody.linearVelocity.x
                );
            }

            bool isWalking =
                velocityX > 0.12f || _movement.IsMoving;

            if (isWalking)
            {
                // Sinkronisasi dengan siklus langkah kaki.
                float cycle = _legController != null
                    ? _legController.WalkCycleNormalized
                    : Time.time * 3f;

                targetOffset =
                    -Mathf.Abs(
                        Mathf.Sin(cycle * Mathf.PI * 2f)
                    ) * _walkBobAmount;
            }
            else
            {
                // Idle breathing.
                targetOffset =
                    Mathf.Sin(Time.time * _breathingFrequency)
                    * _idleBreathingAmount;
            }
        }
        else
        {
            // Tidak melakukan bobbing saat airborne.
            targetOffset = 0f;
        }

        // Batasi pergerakan dari posisi awal.
        targetOffset = Mathf.Clamp(
            targetOffset,
            -_maxPelvisDeviation,
            _maxPelvisDeviation
        );

        // Smooth transition.
        _currentVerticalOffset = Mathf.Lerp(
            _currentVerticalOffset,
            targetOffset,
            Time.deltaTime * _smoothFollowSpeed
        );

        transform.localPosition =
            _initialLocalPosition +
            Vector3.up * _currentVerticalOffset;
    }

    private void UpdatePelvisRotation()
    {
        if (_movement == null ||
            _movement.PlayerRigidbody == null)
            return;

        float currentVelocityX =
            _movement.PlayerRigidbody.linearVelocity.x;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        float accelerationX =
            (currentVelocityX - _lastVelocityX) / deltaTime;

        _lastVelocityX = currentVelocityX;

        float facingDir = 1f;

        if (_facingReference != null)
        {
            facingDir = Mathf.Sign(
                _facingReference.lossyScale.x
            );

            if (facingDir == 0f)
                facingDir = 1f;
        }

        float targetTilt = 0f;

        // Body lean saat bergerak.
        if (Mathf.Abs(currentVelocityX) > 0.15f)
        {
            targetTilt =
                -Mathf.Sign(currentVelocityX) *
                Mathf.Clamp(
                    Mathf.Abs(currentVelocityX) * _tiltSensitivity,
                    0f,
                    _maxTiltAngle
                );
        }
        // Respons ketika mengerem.
        else if (Mathf.Abs(accelerationX) > 2f)
        {
            targetTilt = Mathf.Sign(accelerationX) * 3f;
        }

        targetTilt *= facingDir;

        _currentTiltAngle = Mathf.Lerp(
            _currentTiltAngle,
            targetTilt,
            Time.deltaTime * _tiltRecoverySpeed
        );

        // Rotasi relatif terhadap bind pose awal.
        transform.localRotation =
            _initialLocalRotation *
            Quaternion.Euler(0f, 0f, _currentTiltAngle);
    }
}
