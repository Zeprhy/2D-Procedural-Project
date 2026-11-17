using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    public static CameraFollow2D Instance { get; private set; }

    [Header("Target")]
    [SerializeField] private Transform _target;

    [Header("Pengaturan Follow")]
    [SerializeField] private float _smoothTime = 0.18f;
    [SerializeField] private Vector3 _offset = new Vector3(0f, 1.5f, -10f);

    [Header("Pengaturan Zoom Punch")]
    [SerializeField] private float _defaultZoom = 5f;
    [SerializeField] private float _zoomRestoreSpeed = 6f;

    private Camera _cam;
    private Vector3 _currentVelocity = Vector3.zero;
    private float _targetZoom;

    // Variabel Shake
    private float _shakeIntensity;
    private float _shakeTimer;
    private float _shakeDuration;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _cam = GetComponent<Camera>();
        if (_cam != null)
        {
            _defaultZoom = _cam.orthographicSize;
            _targetZoom = _defaultZoom;
        }
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 desiredPosition = _target.position + _offset;

        Vector3 shakeOffset = Vector3.zero;
        if (_shakeTimer > 0)
        {
            _shakeTimer -= Time.unscaledDeltaTime;
            
            // Goyangan acak cepat
            float currentPower = (_shakeTimer / _shakeDuration) * _shakeIntensity;
            shakeOffset = new Vector3(Random.Range(-1f, 1f) * currentPower, Random.Range(-1f, 1f) * currentPower, 0f);
        }

        Vector3 finalPos = desiredPosition + shakeOffset;
        transform.position = Vector3.SmoothDamp(transform.position, finalPos, ref _currentVelocity, _smoothTime);

        if (_cam != null && _cam.orthographic)
        {
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _defaultZoom, Time.unscaledDeltaTime * _zoomRestoreSpeed);
        }
    }

    public void TriggerHitImpact(float weaponWeight, bool isCritical)
    {
        // Hitung kekuatan guncangan berdasarkan bobot senjata
        float baseIntensity = weaponWeight * 0.05f;
        _shakeIntensity = isCritical ? (baseIntensity * 2.2f) : baseIntensity;
        _shakeDuration = isCritical ? 0.22f : 0.1f;
        _shakeTimer = _shakeDuration;

        if (isCritical && _cam != null && _cam.orthographic)
        {
            float zoomPunchAmount = weaponWeight * 0.06f; // Semakin berat, zoom semakin dekat
            _cam.orthographicSize = Mathf.Max(2.5f, _defaultZoom - zoomPunchAmount);
        }
    }
}