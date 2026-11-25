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

    [Header("Look Ahead - Cursor")]
    [Tooltip("Pengali 0..1 dari jarak look-ahead maksimum")]
    [Range(0f, 1f)]
    [SerializeField] private float _lookAheadStrength = 1f;
    [Tooltip("Pengali vertikal (lebih kecil dari horizontal agar tidak pusing)")]
    [SerializeField] private float _verticalMultiplier = 0.45f;
    [Tooltip("Radius deadzone di tengah layar (0..1) agar gerak kecil tidak menggoyang kamera")]
    [Range(0f, 0.9f)]
    [SerializeField] private float _lookDeadzone = 0.12f;
    [SerializeField] private float _lookSmoothTime = 0.15f;
    [Tooltip("Batas look-ahead kursor dalam world unit (X horizontal, Y vertikal)")]
    [SerializeField] private Vector2 _maxLookOffset = new Vector2(4f, 2.2f);

    [Header("Look Ahead - Velocity")]
    [Tooltip("Faktor antisipasi arah gerak (dikali kecepatan player)")]
    [SerializeField] private float _velocityLookAhead = 0.10f;
    [SerializeField] private Vector2 _maxVelocityLookOffset = new Vector2(3f, 1.5f);
    [SerializeField] private float _velocitySmoothTime = 0.20f;

    [Header("Vertical Damping")]
    [Tooltip("0 = ikut penuh, 0.85 = hampir dikunci saat di tanah")]
    [Range(0f, 1f)]
    [SerializeField] private float _groundedVerticalDamp = 0.85f;

    [Header("Level Bounds")]
    [SerializeField] private bool _clampToBounds = true;
    [SerializeField] private Collider2D _boundsCollider;

    private Camera _cam;
    private Vector3 _currentVelocity = Vector3.zero;
    private Vector3 _smoothFollowPos;

    // State Shake
    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;

    // State Zoom Punch
    private float _currentZoomPunch;

    // State Look Ahead
    private Vector2 _lookOffset;
    private Vector2 _lookVelocity;
    private Vector2 _velOffset;
    private Vector2 _velVelocity;

    // Cache komponen player
    private Rigidbody2D _targetRb;
    private PlatformerMovement _movement;

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
            _target.TryGetComponent(out _targetRb);
            _target.TryGetComponent(out _movement);

            _smoothFollowPos = _target.position + _offset;
            transform.position = _smoothFollowPos;
        }
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        // 1. Look Ahead Kursor (dihitung dari viewport space agar tidak terjadi
        //    feedback loop antara gerak kamera dan posisi kursor)
        Vector2 cursorLook = Vector2.zero;
        if (_cam != null)
        {
            Vector2 vp = _cam.ScreenToViewportPoint(Input.mousePosition);
            Vector2 fromCenter = (vp - new Vector2(0.5f, 0.5f)) * 2f;

            float mag = fromCenter.magnitude;
            Vector2 dir = mag > 0.0001f ? fromCenter / mag : Vector2.zero;

            // Remap melewati deadzone tengah layar
            float effective = Mathf.Clamp01((mag - _lookDeadzone) / Mathf.Max(0.0001f, 1f - _lookDeadzone));

            Vector2 rawLook = new Vector2(
                dir.x * effective * _maxLookOffset.x,
                dir.y * effective * _maxLookOffset.y * _verticalMultiplier);

            rawLook *= _lookAheadStrength;
            _lookOffset = Vector2.SmoothDamp(_lookOffset, rawLook, ref _lookVelocity, _lookSmoothTime);
            cursorLook = _lookOffset;
        }

        // 2. Look Ahead Kecepatan (antisipasi arah gerak / lompat / jatuh)
        Vector2 vel = _targetRb != null ? _targetRb.linearVelocity : Vector2.zero;
        Vector2 rawVel = new Vector2(
            Mathf.Clamp(vel.x * _velocityLookAhead, -_maxVelocityLookOffset.x, _maxVelocityLookOffset.x),
            Mathf.Clamp(vel.y * _velocityLookAhead, -_maxVelocityLookOffset.y, _maxVelocityLookOffset.y));

        _velOffset = Vector2.SmoothDamp(_velOffset, rawVel, ref _velVelocity, _velocitySmoothTime);

        // 3. Redam gerak vertikal saat player di tanah (hilangkan bob saat lari)
        Vector2 lookTotal = cursorLook + _velOffset;
        if (_movement != null && _movement.IsGrounded)
        {
            lookTotal.y *= (1f - _groundedVerticalDamp);
        }

        // 4. Hitung Posisi Follow Halus (bebas dari gangguan getaran)
        Vector3 desiredPosition = _target.position + _offset + (Vector3)lookTotal;
        _smoothFollowPos = Vector3.SmoothDamp(_smoothFollowPos, desiredPosition, ref _currentVelocity, _smoothTime);

        // 5. Hitung Shake Offset (Realtime, tidak terpengaruh Time.timeScale)
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

        // 6. Pemulihan Zoom Punch (dilakukan SEBELUM clamp agar half-extent akurat)
        if (_cam != null && _cam.orthographic)
        {
            _currentZoomPunch = Mathf.MoveTowards(_currentZoomPunch, 0f, Time.unscaledDeltaTime * _zoomRestoreSpeed);

            float targetSize = Mathf.Clamp(_defaultZoom - _currentZoomPunch, _minOrthographicSize, _defaultZoom);
            _cam.orthographicSize = targetSize;
        }

        // 7. Clamp ke batas level (hanya untuk posisi akhir, bukan _smoothFollowPos,
        //    agar transisi kembali ke dalam bounds tetap mulus)
        Vector3 finalPos = _smoothFollowPos;
        if (_clampToBounds && _boundsCollider != null && _cam != null)
        {
            Bounds b = _boundsCollider.bounds;
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            float minX = b.min.x + halfW;
            float maxX = b.max.x - halfW;
            float minY = b.min.y + halfH;
            float maxY = b.max.y - halfH;

            finalPos.x = minX > maxX ? b.center.x : Mathf.Clamp(finalPos.x, minX, maxX);
            finalPos.y = minY > maxY ? b.center.y : Mathf.Clamp(finalPos.y, minY, maxY);
        }

        // 8. Terapkan posisi akhir: Clamped Follow + Shake Instan
        //    (shake boleh sedikit melewati bounds, itu memang diinginkan)
        transform.position = finalPos + shakeOffset;
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

    private void OnDrawGizmosSelected()
    {
        // Visualisasi batas level
        if (_boundsCollider != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Bounds b = _boundsCollider.bounds;
            Gizmos.DrawWireCube(b.center, b.size);

            // Area kamera efektif (setelah dikurangi half-extent)
            if (_cam != null)
            {
                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;
                Vector3 innerSize = new Vector3(
                    Mathf.Max(0f, b.size.x - halfW * 2f),
                    Mathf.Max(0f, b.size.y - halfH * 2f),
                    0f);
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
                Gizmos.DrawWireCube(b.center, innerSize);
            }
        }

        // Visualisasi arah look-ahead kursor saat runtime
        if (_target != null && Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_target.position, _target.position + (Vector3)_lookOffset);
        }
    }
}
