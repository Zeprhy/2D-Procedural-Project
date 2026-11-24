using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class MeleeTrailVFXController : MonoBehaviour
{
    [Header("Referensi")]
    [Tooltip("TrailRenderer ujung pedang. Kosong = auto-find di GameObject ini / child.")]
    [SerializeField] private TrailRenderer _trail;
    [Tooltip("Transform ujung bilah (Weapon_Tip). Kosong = auto-find child bernama Weapon_Tip.")]
    [SerializeField] private Transform _swingTip;

    [Header("Opsi Kontrol")]
    [Tooltip("Terapkan parameter appearance ke TrailRenderer saat resolusi pertama.")]
    [SerializeField] private bool _applySettingsOnAwake = true;
    [Tooltip("Bersihkan segmen lama saat slash baru dimulai agar tidak ada garis nyambung.")]
    [SerializeField] private bool _clearOnBegin = true;

    [Header("Layer 1 - Sword Trail")]
    [SerializeField] private float _trailTime = 0.22f;
    [SerializeField] private float _startWidth = 0.14f;
    [SerializeField] private float _endWidth = 0.01f;
    [Tooltip("Width Curve adalah multiplier terhadap Start Width, bukan world units.")]
    [SerializeField] private bool _useWidthCurve = true;
    [SerializeField] private AnimationCurve _widthCurve = new AnimationCurve(
        new Keyframe(0f, 0.85f),
        new Keyframe(0.18f, 1.15f),
        new Keyframe(0.55f, 0.70f),
        new Keyframe(1f, 0f));
    [Tooltip("Gradient warna trail. Kosong = gradient default.")]
    [SerializeField] private Gradient _colorGradient;
    [Tooltip("Material trail. Kosong = pertahankan material existing.")]
    [SerializeField] private Material _trailMaterial;
    [SerializeField] private float _minVertexDistance = 0.008f;
    [SerializeField] private bool _initialEmitting = false;

    [Header("Layer 2 - Ghost Slash Arc")]
    [SerializeField] private bool _enableGhostSlash = true;
    [Tooltip("Prefab opsional (SpriteRenderer + GhostSlashArc). Kosong = instance prosedural.")]
    [SerializeField] private GameObject _ghostSlashPrefab;
    [Tooltip("Sprite slash opsional. Kosong = sprite prosedural built-in (atau sprite bawaan prefab).")]
    [SerializeField] private Sprite _ghostSlashSprite;
    [Tooltip("Material untuk instance prosedural. Kosong = Sprites/Default.")]
    [SerializeField] private Material _arcMaterial;
    [SerializeField] private Color _arcColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private float _arcLifetime = 0.28f;
    [Tooltip("Panjang chord minimum agar arc muncul (mencegah arc pada swing kecil).")]
    [SerializeField] private float _minArcChord = 0.25f;
    [SerializeField] private float _arcScalePerUnit = 1.15f;
    [SerializeField] private float _arcMinScale = 0.6f;
    [SerializeField] private float _arcMaxScale = 2.4f;
    [SerializeField] private float _arcThicknessRatio = 1f;
    [Tooltip("Centang jika lengkungan crescent terbalik terhadap arah ayunan.")]
    [SerializeField] private bool _invertArcFlip = false;
    [SerializeField] private AnimationCurve _arcAlphaCurve = new AnimationCurve(
        new Keyframe(0f, 0.9f),
        new Keyframe(0.35f, 0.55f),
        new Keyframe(1f, 0f));
    [SerializeField] private AnimationCurve _arcScaleCurve = new AnimationCurve(
        new Keyframe(0f, 0.85f),
        new Keyframe(0.25f, 1f),
        new Keyframe(1f, 1.08f));
    [Tooltip("Sorting Layer untuk Ghost Slash. Kosong = gunakan setting prefab.")]
    [SerializeField] private string _arcSortingLayerName = "";
    [Tooltip("Jika aktif, override Order in Layer prefab.")]
    [SerializeField] private bool _overrideArcSortingOrder = false;
    [SerializeField] private int _arcSortingOrder = 0;
    [SerializeField] private int _maxSwingSamples = 96;
    [SerializeField] private float _sampleMinDistance = 0.02f;

    [Header("Layer 3 - Opsional Swing Sparks")]
    [Tooltip("ParticleSystem opsional (pasang di Weapon_Tip). Kosong = tanpa partikel.")]
    [SerializeField] private ParticleSystem _swingSparks;

    private int _activeSlashCount;
    private bool _trailWarned;
    private bool _trailSettingsApplied;
    private Vector2 _slashAimDirection = Vector2.right;
    private float _slashFacingDirection = 1f;
    private readonly List<Vector3> _swingSamples = new List<Vector3>(96);
    private readonly Queue<GhostSlashArc> _arcPool = new Queue<GhostSlashArc>();
    private readonly List<GhostSlashArc> _activeArcs = new List<GhostSlashArc>();

    public bool IsSlashActive => _activeSlashCount > 0;

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureTrail();
        EnsureTip();

        if (_trail != null)
        {
            _trail.emitting = _initialEmitting;
            if (!_initialEmitting)
                _trail.Clear();
        }
    }

    private void OnEnable()
    {
        // FIX BUG: tidak ada GetComponent di sini.
        // Resolusi referensi hanya lewat EnsureTrail() (lazy + cached + null-safe).
        _activeSlashCount = 0;
        _swingSamples.Clear();

        if (_trail != null)
        {
            _trail.emitting = false;
            _trail.Clear();
        }
    }

    private void OnDisable()
    {
        _activeSlashCount = 0;
        _swingSamples.Clear();

        if (_trail != null)
        {
            _trail.emitting = false;
            _trail.Clear();
        }

        if (_swingSparks != null)
            _swingSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ClearActiveArcs();
    }

    private void OnTransformParentChanged()
    {
        // Weapon equip / drop / switch = parent berubah -> batalkan semua efek.
        CancelSlash();
    }

    private void LateUpdate()
    {
        if (!_enableGhostSlash || _activeSlashCount == 0)
            return;

        Vector3 p = EnsureTip().position;

        if (_swingSamples.Count > 0 &&
            (p - _swingSamples[_swingSamples.Count - 1]).sqrMagnitude < (_sampleMinDistance * _sampleMinDistance))
            return;

        if (_swingSamples.Count >= _maxSwingSamples)
            return;

        _swingSamples.Add(p);
    }

    #endregion

    #region Public API (dipanggil MeleeLogic)

    public void BeginSlash()
    {
        BeginSlash(Vector2.right, 1f);
    }

    public void BeginSlash(Vector2 aimDirection, float facingDirection)
    {
        if (aimDirection.sqrMagnitude > 0.0001f)
            _slashAimDirection = aimDirection.normalized;

        _slashFacingDirection = facingDirection < 0f ? -1f : 1f;
        Transform tip = EnsureTip();

        if (_activeSlashCount == 0)
        {
            _swingSamples.Clear();
            _swingSamples.Add(tip.position);

            if (_swingSparks != null)
            {
                _swingSparks.Clear();
                _swingSparks.Play();
            }
        }

        _activeSlashCount++;

        if (EnsureTrail())
        {
            if (_activeSlashCount == 1 && _clearOnBegin)
                _trail.Clear();

            _trail.emitting = true;
        }
    }

    public void EndSlash()
    {
        if (_activeSlashCount > 0)
            _activeSlashCount--;

        if (_activeSlashCount != 0)
            return;

        if (_trail != null)
            _trail.emitting = false;

        if (_swingSparks != null)
            _swingSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (_enableGhostSlash)
            SpawnGhostSlash();

        _swingSamples.Clear();
    }

    public void CancelSlash()
    {
        _activeSlashCount = 0;
        _swingSamples.Clear();

        if (_trail != null)
        {
            _trail.emitting = false;
            _trail.Clear();
        }

        if (_swingSparks != null)
            _swingSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ClearActiveArcs();
    }

    #endregion

    #region Resolusi Referensi (lazy, cached, null-safe)

    private bool EnsureTrail()
    {
        if (_trail == null)
            _trail = GetComponent<TrailRenderer>();

        if (_trail == null)
            _trail = GetComponentInChildren<TrailRenderer>(true);

        if (_trail == null)
        {
            if (!_trailWarned)
            {
                _trailWarned = true;
                Debug.LogWarning(
                    $"<color=yellow>[MeleeTrailVFXController]</color> TrailRenderer tidak ditemukan pada '{gameObject.name}'. " +
                    "Trail dilewati, ghost slash tetap jalan.", this);
            }
            return false;
        }

        if (_applySettingsOnAwake && !_trailSettingsApplied)
        {
            ApplyTrailSettings();
            _trailSettingsApplied = true;
        }

        return true;
    }

    private Transform EnsureTip()
    {
        if (_swingTip == null)
            _swingTip = transform.Find("Weapon_Tip");

        if (_swingTip == null)
            _swingTip = transform;

        return _swingTip;
    }

    #endregion

    #region Layer 1 - Trail Configuration

    private void ApplyTrailSettings()
    {
        if (_trail == null)
            return;

        _trail.time = Mathf.Max(0.01f, _trailTime);
        _trail.startWidth = Mathf.Max(0.001f, _startWidth);
        _trail.endWidth = Mathf.Max(0.001f, _endWidth);
        _trail.minVertexDistance = Mathf.Max(0.001f, _minVertexDistance);

        if (_useWidthCurve && _widthCurve != null && _widthCurve.length > 0)
            _trail.widthCurve = _widthCurve;

        _trail.colorGradient = (_colorGradient != null) ? _colorGradient : CreateDefaultGradient();

        if (_trailMaterial != null)
            _trail.material = _trailMaterial;
        else if (_trail.sharedMaterial == null)
            Debug.LogWarning(
                $"<color=yellow>[MeleeTrailVFXController]</color> TrailRenderer pada '{_trail.gameObject.name}' tanpa material. " +
                "Assign material transparent (Sprites/Default atau sprite shader pipeline Anda).", this);

        // FIX CS0103: enum yang benar adalah LineAlignment / LineTextureMode
        _trail.alignment = LineAlignment.View;
        _trail.textureMode = LineTextureMode.Stretch;

        // Polish: lengkungan lebih halus
        _trail.numCornerVertices = 8;
        _trail.numCapVertices = 4;

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
            new GradientAlphaKey(0.95f, 0f),
            new GradientAlphaKey(0.60f, 0.40f),
            new GradientAlphaKey(0f, 1f)
        };

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    #endregion

    #region Layer 2 - Ghost Slash Arc

    private void SpawnGhostSlash()
    {
        int n = _swingSamples.Count;
        if (n < 3)
            return;

        Vector3 first = _swingSamples[0];
        Vector3 last = _swingSamples[n - 1];
        Vector3 chord = last - first;
        chord.z = 0f;

        float chordLen = chord.magnitude;
        if (chordLen < _minArcChord)
            return;

        // Tanda arah lengkungan (cw / ccw) dari akumulasi cross product antar segmen
        float crossSum = 0f;
        for (int i = 1; i < n - 1; i++)
        {
            Vector3 d1 = _swingSamples[i] - _swingSamples[i - 1];
            Vector3 d2 = _swingSamples[i + 1] - _swingSamples[i];
            crossSum += d1.x * d2.y - d1.y * d2.x;
        }

        Vector3 anchor = _swingSamples[n / 2];
        anchor.z = 0f;

        Vector2 orientation = _slashAimDirection.sqrMagnitude > 0.0001f
            ? _slashAimDirection.normalized
            : new Vector2(chord.x, chord.y).normalized;

        float angle = Mathf.Atan2(orientation.y, orientation.x) * Mathf.Rad2Deg;

        // Aim menentukan arah world-space. Facing menormalkan sisi lengkungan agar
        // hasil ayunan kiri/kanan tetap menjadi pasangan yang konsisten.
        float flip = (crossSum >= 0f) ? 1f : -1f;
        flip *= _slashFacingDirection;
        if (_invertArcFlip)
            flip = -flip;

        float scaleX = Mathf.Clamp(chordLen * _arcScalePerUnit, _arcMinScale, _arcMaxScale);
        Vector3 scale = new Vector3(
            scaleX,
            scaleX * _arcThicknessRatio * flip,
            1f);

        GhostSlashArc arc = AcquireArc();
        if (arc == null)
            return;

        arc.gameObject.SetActive(true);
        arc.transform.SetParent(null, false);
        arc.transform.position = anchor;
        arc.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        arc.transform.localScale = scale;

        SpriteRenderer arcRenderer = arc.GetComponent<SpriteRenderer>();
        if (arcRenderer != null)
        {
            if (_arcMaterial != null)
                arcRenderer.sharedMaterial = _arcMaterial;

            if (!string.IsNullOrEmpty(_arcSortingLayerName))
                arcRenderer.sortingLayerName = _arcSortingLayerName;

            if (_overrideArcSortingOrder)
                arcRenderer.sortingOrder = _arcSortingOrder;
        }

        Sprite sprite = ResolveArcSprite();
        _activeArcs.Add(arc);

        if (!arc.Play(sprite, _arcColor, _arcLifetime, _arcAlphaCurve, _arcScaleCurve, OnArcFinished))
        {
            _activeArcs.Remove(arc);
            arc.ForceStop();
            arc.gameObject.SetActive(false);
            _arcPool.Enqueue(arc);
        }
    }

    private Sprite ResolveArcSprite()
    {
        if (_ghostSlashSprite != null)
            return _ghostSlashSprite;

        // Jika memakai prefab, biarkan sprite bawaan prefab dipakai (null = jangan override)
        if (_ghostSlashPrefab != null)
            return null;

        return GhostSlashSpriteFactory.GetOrCreate();
    }

    private GhostSlashArc AcquireArc()
    {
        while (_arcPool.Count > 0)
        {
            GhostSlashArc pooled = _arcPool.Dequeue();
            if (pooled != null)
                return pooled;
        }

        if (_ghostSlashPrefab != null)
        {
            GameObject go = Instantiate(_ghostSlashPrefab);
            GhostSlashArc arc = go.GetComponent<GhostSlashArc>();
            if (go.GetComponent<SpriteRenderer>() == null)
                go.AddComponent<SpriteRenderer>();

            if (arc == null)
                arc = go.AddComponent<GhostSlashArc>();
            return arc;
        }

        GameObject procedural = new GameObject("GhostSlashArc_Procedural", typeof(SpriteRenderer), typeof(GhostSlashArc));
        SpriteRenderer sr = procedural.GetComponent<SpriteRenderer>();

        // Biarkan Unity memakai default SpriteRenderer material jika tidak ada override.
        // Hindari new Material() runtime yang memerlukan lifecycle cleanup.
        if (_arcMaterial != null)
            sr.sharedMaterial = _arcMaterial;

        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;

        return procedural.GetComponent<GhostSlashArc>();
    }

    private void OnArcFinished(GhostSlashArc arc)
    {
        _activeArcs.Remove(arc);
        arc.ForceStop();
        arc.transform.SetParent(transform, false);
        arc.gameObject.SetActive(false);
        _arcPool.Enqueue(arc);
    }

    private void ClearActiveArcs()
    {
        for (int i = _activeArcs.Count - 1; i >= 0; i--)
        {
            GhostSlashArc arc = _activeArcs[i];
            if (arc == null)
                continue;

            arc.ForceStop();
            arc.transform.SetParent(transform, false);
            arc.gameObject.SetActive(false);
            _arcPool.Enqueue(arc);
        }

        _activeArcs.Clear();
    }

    #endregion
}