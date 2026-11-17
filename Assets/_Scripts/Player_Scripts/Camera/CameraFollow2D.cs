using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    public static CameraFollow2D Instance { get; private set; }

    [Header("Target & Tracking")]
    [SerializeField] private Transform _target;
    [SerializeField] private float _smoothTime = 0.15f;
    [SerializeField] private Vector3 _offset = new Vector3(0f, 1.2f, -10f);

    [Header("Pengaturan Camera Shake")]
    [Tooltip("Batas intensitas shake maksimum agar layar tidak pusing")]
    [SerializeField] private float _maxShakeIntensity = 0.6f;
    [SerializeField] private float _shakeFrequency = 45f; // Frekuensi getaran per detik

    [Header("Pengaturan Zoom Punch")]
    [SerializeField] private float _defaultZoom = 5f;
    [Tooltip("Kecepatan kamera kembali ke zoom normal setelah freeze selesai")]
    [SerializeField] private float _zoomRestoreSpeed = 7f;
    [Tooltip("Batas zoom in terdekat saat pukulan critical")]
    [SerializeField] private float _minOrthographicSize = 3.2f;

    private Camera _cam;
    private Vector3 _currentVelocity = Vector3.zero;
    private Vector3 _smoothFollowPos;

    // State Shake
    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;

    // State Zoom Punch
    private float _currentZoomPunch;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        _cam = GetComponent<Camera>();
        if (_cam != null && _cam.orthographic)
        {
            _defaultZoom = _cam.orthographicSize;
        }

        if (_target != null)
        {
            _smoothFollowPos = _target.position + _offset;
            transform.position = _smoothFollowPos;
        }
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        // 1. Hitung Posisi Follow Halus (Bebas dari gangguan getaran)
        Vector3 desiredPosition = _target.position + _offset;
        _smoothFollowPos = Vector3.SmoothDamp(_smoothFollowPos, desiredPosition, ref _currentVelocity, _smoothTime);

        // 2. Hitung Shake Offset (Realtime, tidak terpengaruh Time.timeScale)
        Vector3 shakeOffset = Vector3.zero;
        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_shakeTimer / _shakeDuration);

            // Redam intensitas secara kuadratik (smooth falloff)
            float currentPower = _shakeIntensity * (progress * progress);

            // Getaran tajam menggunakan pola sinus/kombinasi noise berkecepatan tinggi
            float shakeX = (Mathf.PerlinNoise(Time.unscaledTime * _shakeFrequency, 0f) * 2f - 1f) * currentPower;
            float shakeY = (Mathf.PerlinNoise(0f, Time.unscaledTime * _shakeFrequency) * 2f - 1f) * currentPower;

            shakeOffset = new Vector3(shakeX, shakeY, 0f);
        }

        // Terapkan posisi akhir: Smooth Follow + Shake Instan
        transform.position = _smoothFollowPos + shakeOffset;

        // 3. Hitung Pemulihan Zoom Punch (Smooth Restoration)
        if (_cam != null && _cam.orthographic)
        {
            // Pulihkan zoom punch menuju 0 secara bertahap
            _currentZoomPunch = Mathf.MoveTowards(_currentZoomPunch, 0f, Time.unscaledDeltaTime * _zoomRestoreSpeed);
            
            float targetSize = Mathf.Clamp(_defaultZoom - _currentZoomPunch, _minOrthographicSize, _defaultZoom);
            _cam.orthographicSize = targetSize;
        }
    }

    /// <summary>
    /// Dipanggil dari WeaponLogic saat serangan mengenai sasaran
    /// </summary>
    public void TriggerHitImpact(float weaponWeight, bool isCritical)
    {
        float weightMod = Mathf.Max(0.5f, weaponWeight / 5f);

        // 1. Konfigurasi Durasi & Intensitas Shake
        float newIntensity = isCritical ? (0.28f * weightMod) : (0.12f * weightMod);
        float newDuration = isCritical ? 0.16f : 0.08f;

        // Amankan agar getaran tidak menumpuk melebihi batas wajar jika multi-hit
        _shakeIntensity = Mathf.Min(_maxShakeIntensity, Mathf.Max(_shakeIntensity, newIntensity));
        _shakeDuration = newDuration;
        _shakeTimer = newDuration;

        // 2. Konfigurasi Zoom Punch
        if (_cam != null && _cam.orthographic)
        {
            // Serangan biasa = zoom punch mikro, Critical = zoom punch dramatis
            float punchAmount = isCritical ? (0.55f * weightMod) : (0.12f * weightMod);

            // Ambil nilai punch terbesar (anti-stacking)
            _currentZoomPunch = Mathf.Max(_currentZoomPunch, punchAmount);
        }
    }
}