using UnityEngine;

[DisallowMultipleComponent]
public class MeleeTrailVFXController : MonoBehaviour
{
    [Header("Referensi Trail")]
    [Tooltip("TrailRenderer yang mengikuti ujung pedang. Jika kosong, akan dicari dari GameObject ini atau child.")]
    [SerializeField] private TrailRenderer _trail;

    [Header("Opsi Kontrol")]
    [Tooltip("Jika aktif, komponen akan menerapkan parameter appearance di bawah ke TrailRenderer saat Awake.")]
    [SerializeField] private bool _applySettingsOnAwake = true;

    [Tooltip("Jika true, segmen lama dibersihkan saat slash baru dimulai agar tidak muncul garis dari posisi sebelumnya.")]
    [SerializeField] private bool _clearOnBegin = true;

    [Header("Trail Appearance")]
    [Tooltip("Durasi hidup segmen trail dalam detik.")]
    [SerializeField] private float _trailTime = 0.18f;

    [Tooltip("Lebar trail pada titik terbaru / kepala trail.")]
    [SerializeField] private float _startWidth = 0.12f;

    [Tooltip("Lebar trail pada ujung paling tua / ekor trail.")]
    [SerializeField] private float _endWidth = 0.02f;

    [Tooltip("Gradient warna trail. Jika kosong, gradient default akan dipakai.")]
    [SerializeField] private Gradient _colorGradient;

    [Tooltip("Material TrailRenderer. Gunakan material transparent yang kompatibel dengan 2D.")]
    [SerializeField] private Material _trailMaterial;

    [Tooltip("Jarak minimum pergerakan sebelum vertex baru dibuat.")]
    [SerializeField] private float _minVertexDistance = 0.01f;

    [Tooltip("Status emitting awal. Sebaiknya false; trail hanya menyala saat serangan.")]
    [SerializeField] private bool _initialEmitting = false;

    private int _activeSlashCount;

    public bool IsSlashActive => _activeSlashCount > 0;

    private void Reset()
    {
        _trail = GetComponent<TrailRenderer>();

        if (_trail == null) _trail = GetComponentInChildren<TrailRenderer>();
        if (_colorGradient == null)
            _colorGradient = CreateDefaultGradient();
    }

    private void Awake()
    {
        if (_trail == null)
            _trail = GetComponent<TrailRenderer>();

        if (_trail == null)
            _trail = GetComponentInChildren<TrailRenderer>(true);

        if (_trail == null)
        {
            Debug.LogWarning(
                $"<color=yellow>[MeleeTrailVFXController]</color> TrailRenderer tidak ditemukan pada '{gameObject.name}'. " +
                "Pastikan TrailRenderer berada pada Weapon_Tip atau child dari weapon root.",
                this);

            enabled = false;
            return;
        }

        if (_applySettingsOnAwake)
            ApplyTrailSettings();

        _trail.emitting = _initialEmitting;

        if (!_initialEmitting)
            _trail.Clear();
    }

    private void OnEnable()
    {
        _activeSlashCount = 0;

        if (_trail == null)
            _trail.GetComponent<TrailRenderer>();
        
        if (_trail == null)
            _trail.GetComponentInChildren<TrailRenderer>();

        if (_trail != null)
        {
            _trail.emitting = false;
            _trail.Clear();
        }
    }

    private void OnDisable()
    {
        _activeSlashCount = 0;

        if (_trail != null)
        {
            _trail.emitting = false;
            _trail.Clear();
        }
    }

    private void OnTransformParentChanged() => CancelSlash();
    
    /// <summary>
    /// Dipanggil saat melee slash dimulai.
    /// </summary>
    public void BeginSlash()
    {
        if (_trail == null || !_trail.enabled || !_trail.gameObject.activeInHierarchy) return;

        if (_activeSlashCount == 0 && _clearOnBegin)
            _trail.Clear();

        _activeSlashCount++;
        _trail.emitting = true;
    }


    /// <summary>
    /// Dipanggil saat melee slash selesai.
    /// Segmen trail yang sudah terbentuk tetap dibiarkan memudar sesuai Trail Time.
    /// </summary>
    public void EndSlash()
    {
        if (_trail == null)
        {
            _activeSlashCount = 0;
            return;
        }

        if (_activeSlashCount > 0)
            _activeSlashCount--;

        if (_activeSlashCount == 0)
            _trail.emitting = false;
    }

    /// <summary>
    /// Dipanggil saat trail harus dihentikan dan dibersihkan segera,
    /// misalnya ketika weapon disable, weapon switch, atau cancel.
    /// </summary>
    public void CancelSlash()
    {
        _activeSlashCount = 0;

        if (_trail == null) return;

        _trail.emitting = false;
        _trail.Clear();
    }

    private void ApplyTrailSettings()
    {
        _trail.time = Mathf.Max(0.01f, _trailTime);
        _trail.startWidth = Mathf.Max(0.001f, _startWidth);
        _trail.endWidth = Mathf.Max(0.001f, _endWidth);
        _trail.minVertexDistance = Mathf.Max(0.001f, _minVertexDistance);

        if (_colorGradient != null)
            _trail.colorGradient = _colorGradient;
        else
            _trail.colorGradient = CreateDefaultGradient();

        if (_trailMaterial != null )
            _trail.material = _trailMaterial;
        else if (_trail.sharedMaterial == null)
        {
            Debug.LogWarning(
                $"<color=yellow>[MeleeTrailVFXController]</color> TrailRenderer pada '{_trail.gameObject.name}' tidak memiliki material. " +
                "Assign material transparent seperti Sprites/Default atau material 2D lain.",
                this);
        }

        _trail.alignment = LineAlignment.View;
        _trail.textureMode = LineTextureMode.Stretch;
        _trail.numCornerVertices = 4;
        _trail.numCapVertices = 2;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
    }

    private static Gradient CreateDefaultGradient()
    {   
        Gradient gradient = new Gradient();

        GradientColorKey[] colorKeys = new GradientColorKey[3]
        {
            new GradientColorKey(new Color(1f, 1f, 1f, 1f), 0f),
            new GradientColorKey(new Color(0.85f, 0.95f, 1f, 1f), 0.55f),
            new GradientColorKey(new Color(0.65f, 0.85f, 1f, 1f), 1f)
        };

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3]
        {
            new GradientAlphaKey (0.9f, 0f),
            new GradientAlphaKey (0.55f, 0.6f),
            new GradientAlphaKey (0f, 1f)
        };

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }
}