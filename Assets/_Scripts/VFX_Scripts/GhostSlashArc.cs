using UnityEngine;
using System.Collections;

public class GhostSlashArc : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _renderer;

    private Coroutine _routine;
    private Vector3 _baseScale;
    private System.Action<GhostSlashArc> _onFinished;

    private void Awake()
    {
        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// sprite = null berarti pertahankan sprite yang sudah ada di renderer (mis. bawaan prefab).
    /// </summary>
    public void Play(Sprite sprite, Color color, float lifetime, AnimationCurve alphaCurve,
                     AnimationCurve scaleCurve, System.Action<GhostSlashArc> onFinished)
    {
        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();

        if (_renderer == null || lifetime <= 0f)
        {
            if (onFinished != null) onFinished(this);
            return;
        }

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (sprite != null)
            _renderer.sprite = sprite;

        _renderer.enabled = true;
        _renderer.color = color;
        _baseScale = transform.localScale;
        _onFinished = onFinished;

        _routine = StartCoroutine(FadeRoutine(lifetime, alphaCurve, scaleCurve));
    }

    public void ForceStop()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (_renderer != null)
            _renderer.enabled = false;

        _onFinished = null;
    }

    private IEnumerator FadeRoutine(float lifetime, AnimationCurve alphaCurve, AnimationCurve scaleCurve)
    {
        Color c = _renderer.color;
        float t = 0f;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / lifetime);

            c.a = (alphaCurve != null) ? alphaCurve.Evaluate(k) : (1f - k);
            _renderer.color = c;

            float s = (scaleCurve != null) ? scaleCurve.Evaluate(k) : 1f;
            transform.localScale = _baseScale * s;

            yield return null;
        }

        _routine = null;
        System.Action<GhostSlashArc> cb = _onFinished;
        _onFinished = null;

        if (cb != null)
            cb(this);
    }
}

/// <summary>
/// Fallback sprite crescent prosedural (di-cache statis) agar fitur tetap jalan
/// tanpa asset slash sprite. Untuk kualitas final, assign sprite custom di controller.
/// </summary>
public static class GhostSlashSpriteFactory
{
    private const int Size = 256;
    private const float Radius = 0.55f;
    private const float Thickness = 0.12f;
    private const float SpanRad = 55f * Mathf.Deg2Rad;

    private static Sprite _cached;

    public static Sprite GetOrCreate()
    {
        if (_cached != null)
            return _cached;

        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            float v = (y + 0.5f) / Size - 0.5f;

            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size - 0.5f;
                float dy = v + Radius;
                float dist = Mathf.Sqrt(u * u + dy * dy);

                float band = 1f - Mathf.Clamp01(Mathf.Abs(dist - Radius) / Thickness);
                band = Mathf.SmoothStep(0f, 1f, band);

                float ang = Mathf.Atan2(u, dy);
                float edge = 1f - Mathf.Clamp01(Mathf.Abs(ang) / SpanRad);
                edge = Mathf.SmoothStep(0f, 1f, edge);

                byte a = (byte)Mathf.RoundToInt(255f * band * edge);
                pixels[y * Size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        // ppu 256 => sprite berukuran native 1 world unit, mudah di-scale lewat chord length
        _cached = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 256f);
        _cached.name = "GhostSlashArc_Procedural";

        return _cached;
    }
}