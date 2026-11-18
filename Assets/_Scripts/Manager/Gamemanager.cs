using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)] // Memastikan GameManager siap sebelum script gameplay lainnya berjalan
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Lifecycle Settings")]
    [Tooltip("Centang jika GameManager ingin bertahan saat berpindah scene")]
    [SerializeField] private bool _dontDestroyOnLoad = false;

    [Header("Equipment & Combat Managers")]
    [Tooltip("Manager equipment senjata pemain")]
    [SerializeField] private WeaponEquipmentManager _weaponEquipmentManager;

    public WeaponEquipmentManager WeaponEquipmentManager {get; private set;}

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

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Start()
    {
        if (_hasInitialized) return;
        InitializeManagers();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _hasInitialized = false;
        InitializeManagers();
    }

    /// <summary>
    /// Menginisialisasi referensi manager dengan fallback otomatis (Pola arsitektur modular)
    /// </summary>
    private void InitializeManagers()
    {
        if (_hasInitialized) return;
        _hasInitialized = true;

        // 1. Cari WeaponEquipmentManager jika belum di-assign di Inspector
        // Cek dulu apakah ada di child GameManager, jika tidak ada cari di Player scene
        if (_weaponEquipmentManager == null)
        {
            _weaponEquipmentManager = GetComponentInChildren<WeaponEquipmentManager>();
            if (_weaponEquipmentManager == null)
            {
                _weaponEquipmentManager = FindFirstObjectByType<WeaponEquipmentManager>();
            }
        }

        // CATATAN UNTUK STAGE BERIKUTNYA:
        // Di Stage 3 & 4 nanti, ItemDropManager dan LootManager akan kita tambahkan di sini
        // persis mengikuti pola yang sama:
        // if (_itemDropManager == null) _itemDropManager = GetComponentInChildren<ItemDropManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterWeaponEquipmentManager(WeaponEquipmentManager manager)
    {
        if (WeaponEquipmentManager != null && WeaponEquipmentManager != manager)
        {
            Debug.LogWarning($"<color=yellow>[GameManager]</color> Mengganti registrasi WeaponEquipmentManager lama dengan instance baru.", this);
        }

        WeaponEquipmentManager = manager;
    }

    public void UnregisterWeaponEquipmentManager(WeaponEquipmentManager manager)
    {
        if (WeaponEquipmentManager == manager)
        {
            WeaponEquipmentManager = null;
        }
    }
}