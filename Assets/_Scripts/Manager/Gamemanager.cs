using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)] // Memastikan GameManager siap sebelum script player/gameplay berjalan
public class GameManager : MonoBehaviour
{
    // HANYA GameManager yang boleh mendeklarasikan static Instance (Strict Singleton)
    public static GameManager Instance { get; private set; }

    [Header("Lifecycle Settings")]
    [Tooltip("Centang jika GameManager ingin bertahan saat berpindah scene")]
    [SerializeField] private bool _dontDestroyOnLoad = false;

    [Header("Equipment & Combat Managers")]
    [Tooltip("Referensi WeaponEquipmentManager aktif (diisi via Inspector atau otomatis saat Player spawn)")]
    [SerializeField] private WeaponEquipmentManager _weaponEquipmentManager;

    // CORRECTION 1 & 2: Properti publik dengan 'private set' yang sinkron 100% dengan backing field
    public WeaponEquipmentManager WeaponEquipmentManager
    {
        get => _weaponEquipmentManager;
        private set => _weaponEquipmentManager = value;
    }

    private bool _hasInitialized = false;

    private void Awake()
    {
        // Enforce Strict Singleton
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"<color=yellow>[GameManager]</color> Duplikat GameManager terdeteksi pada '{gameObject.name}'. Menghancurkan duplikat.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_dontDestroyOnLoad)
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (_hasInitialized) return;
        InitializeManagers();
    }

    // CORRECTION 3: Penanganan pergantian scene yang aman dari stale reference
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Bersihkan referensi usang jika objek dari scene sebelumnya sudah hancur
        if (_weaponEquipmentManager == null)
        {
            WeaponEquipmentManager = null;
        }

        _hasInitialized = false;
        InitializeManagers();
    }

    /// <summary>
    /// Jalur inisialisasi / fallback jika manager belum terdaftar via OnEnable Player
    /// </summary>
    private void InitializeManagers()
    {
        if (_hasInitialized) return;
        _hasInitialized = true;

        // Jika properti masih kosong (atau referensi lama hancur), lakukan pencarian fallback di scene baru
        if (WeaponEquipmentManager == null)
        {
            WeaponEquipmentManager found = GetComponentInChildren<WeaponEquipmentManager>();
            if (found == null)
            {
                found = FindFirstObjectByType<WeaponEquipmentManager>();
            }

            if (found != null)
            {
                RegisterWeaponEquipmentManager(found);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region Manager Registration

    /// <summary>
    /// CORRECTION 2 & 4: Satu-satunya jalur registrasi resmi untuk WeaponEquipmentManager
    /// </summary>
    public void RegisterWeaponEquipmentManager(WeaponEquipmentManager manager)
    {
        if (manager == null) return;

        if (WeaponEquipmentManager != null && WeaponEquipmentManager != manager)
        {
            Debug.LogWarning($"<color=yellow>[GameManager]</color> Mengganti WeaponEquipmentManager lama dengan instance baru.", this);
        }

        // Sinkronkan backing field dan properti publik secara bersamaan
        WeaponEquipmentManager = manager;
    }

    /// <summary>
    /// CORRECTION 3 & 4: Membersihkan referensi saat Player di-disable atau dihancurkan
    /// </summary>
    public void UnregisterWeaponEquipmentManager(WeaponEquipmentManager manager)
    {
        if (WeaponEquipmentManager == manager)
        {
            WeaponEquipmentManager = null;
        }
    }

    #endregion
}