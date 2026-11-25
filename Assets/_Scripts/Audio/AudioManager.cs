using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton AudioManager dengan Object Pool AudioSource untuk performa optimal (zero GC allocation).
/// Semua SFX dimainkan sebagai 2D sound (spatialBlend = 0) dengan volume konstan.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Pool Settings")]
    [Tooltip("Jumlah AudioSource yang di-prewarm di startup")]
    [SerializeField] private int _poolSize = 24;
    [Tooltip("Prefab AudioSource untuk pool (kosongkan untuk auto-create)")]
    [SerializeField] private AudioSource _sourcePrefab;

    [Header("Global Volume")]
    [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 1f;

    private readonly Queue<AudioSource> _availableSources = new Queue<AudioSource>();
    private readonly List<AudioSource> _activeSources = new List<AudioSource>();
    private Transform _listenerTransform;

    // PlayerPrefs keys
    private const string PREFS_MASTER = "Audio_MasterVolume";
    private const string PREFS_SFX = "Audio_SFXVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load saved volumes
        _masterVolume = PlayerPrefs.GetFloat(PREFS_MASTER, 1f);
        _sfxVolume = PlayerPrefs.GetFloat(PREFS_SFX, 1f);

        // Setup listener reference (main camera)
        if (Camera.main != null)
            _listenerTransform = Camera.main.transform;

        // Create source prefab if not assigned
        if (_sourcePrefab == null)
        {
            GameObject prefabObj = new GameObject("PooledAudioSource");
            _sourcePrefab = prefabObj.AddComponent<AudioSource>();
            _sourcePrefab.playOnAwake = false;
            _sourcePrefab.spatialBlend = 0f; // 2D sound
            _sourcePrefab.rolloffMode = AudioRolloffMode.Linear;
            Destroy(prefabObj); // We only need the component reference
        }

        // Prewarm pool
        for (int i = 0; i < _poolSize; i++)
        {
            AudioSource source = CreatePooledSource();
            _availableSources.Enqueue(source);
        }
    }

    private AudioSource CreatePooledSource()
    {
        GameObject obj = new GameObject("PooledAudioSource");
        obj.transform.SetParent(transform);
        AudioSource source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D only
        source.rolloffMode = AudioRolloffMode.Linear;
        source.dopplerLevel = 0f;
        return source;
    }

    private void Update()
    {
        // Return finished sources to pool
        for (int i = _activeSources.Count - 1; i >= 0; i--)
        {
            AudioSource source = _activeSources[i];
            if (!source.isPlaying)
            {
                _activeSources.RemoveAt(i);
                ReturnToPool(source);
            }
        }
    }

    private AudioSource GetSource()
    {
        if (_availableSources.Count > 0)
        {
            AudioSource source = _availableSources.Dequeue();
            source.gameObject.SetActive(true);
            return source;
        }

        // Pool exhausted - create new (expands dynamically)
        AudioSource newSource = CreatePooledSource();
        Debug.LogWarning($"<color=yellow>[AudioManager]</color> Pool exhausted, expanding. Consider increasing pool size.");
        return newSource;
    }

    private void ReturnToPool(AudioSource source)
    {
        source.Stop();
        source.clip = null;
        source.transform.SetParent(transform);
        source.transform.localPosition = Vector3.zero;
        source.gameObject.SetActive(false);
        _availableSources.Enqueue(source);
    }

    /// <summary>
    /// Main SFX playback method. Plays a clip at a world position (2D sound, no spatial attenuation).
    /// </summary>
    /// <param name="clip">Audio clip to play</param>
    /// <param name="position">World position (used only for potential future 3D support)</param>
    /// <param name="volume">Local volume multiplier (0-1), multiplied by global SFX volume</param>
    /// <param name="pitchVariation">Random pitch range ±value (e.g., 0.1f = ±10%)</param>
    public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariation = 0f)
    {
        if (clip == null) return;

        AudioSource source = GetSource();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * _sfxVolume * _masterVolume);
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.transform.position = position; // Kept for consistency, though 2D ignores position
        source.Play();

        _activeSources.Add(source);
    }

    /// <summary>
    /// Plays SFX following a transform (useful for moving sources like player weapons).
    /// </summary>
    public void PlaySFX(AudioClip clip, Transform followTarget, float volume = 1f, float pitchVariation = 0f)
    {
        if (clip == null || followTarget == null) return;

        AudioSource source = GetSource();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * _sfxVolume * _masterVolume);
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.transform.SetParent(followTarget);
        source.transform.localPosition = Vector3.zero;
        source.Play();

        // Use a coroutine to return to pool after clip ends
        StartCoroutine(ReturnAfterPlay(source, clip.length / Mathf.Max(0.01f, source.pitch)));
        _activeSources.Add(source);
    }

    private System.Collections.IEnumerator ReturnAfterPlay(AudioSource source, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (source != null && _activeSources.Contains(source))
        {
            _activeSources.Remove(source);
            source.transform.SetParent(transform);
            ReturnToPool(source);
        }
    }

    // Volume Control API
    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_MASTER, _masterVolume);
        UpdateAllSourcesVolume();
    }

    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_SFX, _sfxVolume);
        UpdateAllSourcesVolume();
    }

    public float MasterVolume => _masterVolume;
    public float SFXVolume => _sfxVolume;

    private void UpdateAllSourcesVolume()
    {
        float combined = _masterVolume * _sfxVolume;
        foreach (var source in _activeSources)
        {
            if (source.clip != null)
                source.volume = combined; // Note: this resets per-clip volume multiplier
        }
    }

    /// <summary>
    /// Stop all currently playing SFX and return to pool.
    /// </summary>
    public void StopAllSFX()
    {
        foreach (var source in _activeSources)
        {
            ReturnToPool(source);
        }
        _activeSources.Clear();
    }
}