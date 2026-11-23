using UnityEngine;

[DisallowMultipleComponent]
public class PelvisController2DRebuild : MonoBehaviour
{
    [Header("Referensi Root & Karakter")]
    [Tooltip("Transform GameObject Player (Root yang memegang Rigidbody2D)")]
    [SerializeField] private Transform _playerRoot;
    [SerializeField] private PlatformerMovement _movement;
    [SerializeField] private ProceduralBipedLeg2DRebuild _legController;
    [SerializeField] private Transform _facingReference;

    [Header("Tinggi Dasar Pinggul (Neutral Height)")]
    [Tooltip("Ketinggian posisi Y pinggul di atas Player Root saat berdiri tegak")]
    [SerializeField] private float _defaultHeightOffset = 1.25f;
    [Tooltip("Batas maksimum toleransi pergeseran vertikal pinggul (mencegah karakter melayang)")]
    [SerializeField] private float _maxPelvisDeviation = 0.25f;
    [SerializeField] private float _smoothFollowSpeed = 12f;

    [Header("Walk Bobbing (Sinkron dengan Langkah)")]
    [Tooltip("Intensitas naik-turun pinggul saat berjalan")]
    [SerializeField] private float _walkBobAmount = 0.06f;

    [Header("Idle Breathing (Nafas Halus)")]
    [Tooltip("Intensitas nafas halus saat karakter diam")]
    [SerializeField] private float _idleBreathingAmount = 0.025f;
    [Tooltip("Frekuensi nafas per detik")]
    [SerializeField] private float _breathingFrequency = 2.5f;

    [Header("Body Tilt & Inersia")]
    [Tooltip("Sudut kemiringan badan saat akselerasi atau pengereman (derajat)")]
    [SerializeField] private float _maxTiltAngle = 6f;
    [Tooltip("Sensitivitas kemiringan terhadap inersia horizontal")]
    [SerializeField] private float _tiltSensitivity = 0.8f;
    [SerializeField] private float _tiltRecoverySpeed = 10f;

    private Quaternion _initialLocalRotation;
    private float _currentTiltAngle = 0f;
    private float _lastVelocityX = 0f;

    private void Awake()
    {
        _initialLocalRotation = transform.localRotation;

        if (_playerRoot == null)
            _playerRoot = transform.root;

        if (_movement == null && _playerRoot != null)
            _movement = _playerRoot.GetComponent<PlatformerMovement>();

        if (_legController == null && _playerRoot != null)
            _legController = _playerRoot.GetComponent<ProceduralBipedLeg2DRebuild>();

        if (_facingReference == null && _playerRoot != null)
            _facingReference = _playerRoot;
    }

    private void LateUpdate()
    {
        if (_playerRoot == null) return;

        UpdatePelvisPosition();
        UpdatePelvisRotation();
    }

    private void UpdatePelvisPosition()
    {
        // 1. Ketinggian Dasar: Terikat mutlak pada posisi Y Player Root (Bebas dari feedback loop kaki)
        float baseWorldY = _playerRoot.position.y + _defaultHeightOffset;

        float verticalOffset = 0f;

        // 2. Evaluasi Animasi Berdasarkan State
        if (_movement != null && _movement.IsGrounded)
        {
            float vx = Mathf.Abs(_movement.PlayerRigidbody.linearVelocity.x);

            if (vx > 0.12f || _movement.IsMoving)
            {
                // WALK BOB: Sinkron dengan fase langkah kaki
                // Pinggul turun di tengah langkah saat kedua kaki terbuka, dan naik saat satu kaki di puncak ayunan
                float cycle = _legController != null ? _legController.WalkCycleNormalized : (Time.time * 6f);
                verticalOffset = -Mathf.Abs(Mathf.Sin(cycle * Mathf.PI * 2f)) * _walkBobAmount;
            }
            else
            {
                // IDLE BREATHING: Nafas halus sinusoidal
                verticalOffset = Mathf.Sin(Time.time * _breathingFrequency) * _idleBreathingAmount;
            }
        }
        else
        {
            // AIRBORNE: Pinggul sedikit rileks menyerap lompatan
            verticalOffset = 0f;
        }

        // 3. HARD CLAMP: Batasi deviasi vertikal agar pinggul tidak pernah keluar batas aman
        verticalOffset = Mathf.Clamp(verticalOffset, -_maxPelvisDeviation, _maxPelvisDeviation);

        float targetWorldY = baseWorldY + verticalOffset;

        // Terapkan posisi dengan interpolasi halus
        Vector3 currentPos = transform.position;
        currentPos.x = _playerRoot.position.x; // Tetap sejajar horizontal dengan pusat tubuh
        currentPos.y = Mathf.Lerp(currentPos.y, targetWorldY, Time.deltaTime * _smoothFollowSpeed);
        transform.position = currentPos;
    }

    private void UpdatePelvisRotation()
    {
        if (_movement == null) return;

        float currentVx = _movement.PlayerRigidbody.linearVelocity.x;
        float facingDir = _facingReference != null ? Mathf.Sign(_facingReference.lossyScale.x) : 1f;

        // Hitung akselerasi / deselerasi horizontal
        float accelX = (currentVx - _lastVelocityX) / Time.deltaTime;
        _lastVelocityX = currentVx;

        // Hitung sudut kemiringan inersia (condong ke depan saat ngebut, condong ke belakang saat ngerem)
        float targetTilt = 0f;
        if (Mathf.Abs(currentVx) > 0.15f)
        {
            targetTilt = -Mathf.Sign(currentVx) * Mathf.Clamp(Mathf.Abs(currentVx) * _tiltSensitivity, 0f, _maxTiltAngle);
        }
        else if (Mathf.Abs(accelX) > 2f)
        {
            // Respons kejut saat rem mendadak
            targetTilt = Mathf.Sign(accelX) * 3f;
        }

        targetTilt *= facingDir;

        _currentTiltAngle = Mathf.Lerp(_currentTiltAngle, targetTilt, Time.deltaTime * _tiltRecoverySpeed);

        // Terapkan kemiringan di atas rotasi bind-pose lokal tulang
        transform.localRotation = _initialLocalRotation * Quaternion.Euler(0f, 0f, _currentTiltAngle);
    }
}