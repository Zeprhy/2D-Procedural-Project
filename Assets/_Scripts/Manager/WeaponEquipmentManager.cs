using UnityEngine;

public class WeaponEquipmentManager : MonoBehaviour
{
    [Header("Referensi Tangan Kanan")]
    [SerializeField] private RightHandController2D _rightHandController;
    [Tooltip("Titik pasang visual senjata kanan (B_R_Hand -> Item_Holder_R)")]
    [SerializeField] private Transform _rightHandHolder;

    [Header("Referensi Tangan Kiri / Off-Hand ")]
    [SerializeField] private OffHandController2D _offHandController;
    [Tooltip("Titik pasang visual senjata kiri (B_L_Hand -> Item_Holder_L)")]
    [SerializeField] private Transform _leftHandHolder;

    [Header("Senjata Awal (Eksisting / Default)")]
    [Tooltip("WeaponLogic pedang yang sedang digunakan saat ini")]
    [SerializeField] private WeaponLogic _initialWeapon;

    [Header("Pengaturan Drop")]
    [Tooltip("Prefab PF_WorldItem yang akan dimunculkan di dunia saat senjata dijatuhkan")]
    [SerializeField] private WorldItem _worldItemPrefab;
    [Tooltip("Offset posisi spawn item relatif terhadap arah hadap pemain")]
    [SerializeField] private Vector2 _dropOffset = new Vector2(0.5f, 0.3f);
    [Tooltip("Dorongan fisik horizontal saat menjatuhkan senjata")]
    [SerializeField] private float _dropImpulseX = 2.5f;
    [Tooltip("Dorongan fisik vertikal ke atas saat menjatuhkan senjata")]
    [SerializeField] private float _dropImpulseY = 2.0f;
    [Tooltip("Referensi transform arah hadap karakter (misal GFX), fallback ke transform root jika kosong")]
    [SerializeField] private Transform _facingReference;

    // Properti publik untuk status equipment
    public WeaponLogic CurrentWeapon { get; private set; }
    public BaseWeaponSO CurrentWeaponData { get; private set; }
    public bool HasEquippedWeapon => CurrentWeapon != null;
    

    private void Awake()
    {
        if (_rightHandController == null)
            _rightHandController = GetComponentInChildren<RightHandController2D>();

        if (_offHandController == null)
            _offHandController = GetComponentInChildren<OffHandController2D>();

        if (_facingReference == null)
            _facingReference = transform;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterWeaponEquipmentManager(this);
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterWeaponEquipmentManager(this);
        }
    }

    private void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.WeaponEquipmentManager != this)
        {
            GameManager.Instance.RegisterWeaponEquipmentManager(this);
        }

        // Transisi aman: pasang pedang awal ke RightHandController
        if (_initialWeapon != null)
        {
            EquipExistingWeapon(_initialWeapon);
        }
        else if (_rightHandController != null && _rightHandController.EquippedWeapon != null)
        {
            EquipExistingWeapon(_rightHandController.EquippedWeapon);
        }
    }

    public void EquipExistingWeapon(WeaponLogic weapon)
    {
        CurrentWeapon = weapon;

        // Pastikan senjata awal yang terpasang di holder juga mendapatkan grip alignment
        if (_rightHandHolder != null && weapon != null && weapon.transform.IsChildOf(_rightHandHolder))
        {
            AlignWeaponGrip(weapon.transform, "Grip_Point_R");
        }

        if (_rightHandController != null)
        {
            _rightHandController.SetEquippedWeapon(weapon);
        }
    }

    public void UnequipWeapon()
    {
        CurrentWeapon = null;

        if (_rightHandController != null)
        {
            _rightHandController.ClearEquippedWeapon();
        }
    }

    #region Loot Integration API 

    public bool CanEquip(BaseWeaponSO weaponData)
    {
        if (weaponData == null)
        {
            Debug.LogWarning("<color=yellow>[WeaponEquipmentManager]</color> CanEquip ditolak: Data senjata null!", this);
            return false;
        }

        if (weaponData.WeaponPrefab == null)
        {
            Debug.LogWarning($"<color=yellow>[WeaponEquipmentManager]</color> CanEquip ditolak: WeaponPrefab pada '{weaponData.WeaponName}' belum diisi di ScriptableObject!", this);
            return false;
        }

        if (HasEquippedWeapon)
        {
            Debug.Log($"<color=white>[WeaponEquipmentManager]</color> Tangan kanan sudah terisi oleh '{CurrentWeapon.name}'. Lepaskan (Drop) terlebih dahulu sebelum mengambil senjata baru.", this);
            return false;
        }

        return true;
    }

    public bool TryLootWeapon(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.IsConsumed) return false;

        BaseWeaponSO weaponData = worldItem.ItemData;
        if (!CanEquip(weaponData)) return false;

        if (_rightHandHolder == null)
        {
            Debug.LogError("<color=red>[WeaponEquipmentManager]</color> Right Hand Holder belum diatur di Inspector!", this);
            return false;
        }

        // 1. Instansiasi WeaponPrefab sebagai anak langsung dari Item_Holder_R
        GameObject weaponInstance = Instantiate(weaponData.WeaponPrefab, _rightHandHolder);
        weaponInstance.transform.localScale = Vector3.one;

        AlignWeaponGrip(weaponInstance.transform, "Grip_Point_R");

        // 2. Dapatkan komponen WeaponLogic dari instance baru
        WeaponLogic newWeaponLogic = weaponInstance.GetComponent<WeaponLogic>();
        if (newWeaponLogic == null)
            newWeaponLogic = weaponInstance.GetComponentInChildren<WeaponLogic>();

        if (newWeaponLogic == null)
        {
            Debug.LogError($"<color=red>[WeaponEquipmentManager]</color> WeaponPrefab '{weaponData.WeaponPrefab.name}' tidak memiliki komponen WeaponLogic!", this);
            Destroy(weaponInstance);
            return false;
        }

        // 3. Pasang senjata ke controller tangan kanan
        EquipExistingWeapon(newWeaponLogic);

        // 4. Tandai item dunia telah dikonsumsi dan hancurkan dari scene
        worldItem.MarkAsConsumed();
        Destroy(worldItem.gameObject);

        Debug.Log($"<color=green>[WeaponEquipmentManager]</color> Berhasil mengambil dan memasang '{weaponData.WeaponName}' dengan Grip Alignment!", this);
        return true;
    }

    #endregion

    #region Drop System AP
    /// <summary>
    /// Menjatuhkan senjata yang sedang aktif ke dunia game sebagai WorldItem
    /// </summary>
    public bool DropWeapon()
    {
        if (!HasEquippedWeapon)
            return false;

        BaseWeaponSO weaponData = CurrentWeaponData;
        if (weaponData == null && CurrentWeapon != null)
            weaponData = CurrentWeapon.WeaponData;

        if (weaponData == null)
            return false;

        if (_worldItemPrefab == null)
            return false;

        float facingDir = _facingReference != null ? Mathf.Sign(_facingReference.lossyScale.x) : Mathf.Sign(transform.lossyScale.x);
        Vector3 spawnPosition = transform.position + new Vector3(_dropOffset.x * facingDir, _dropOffset.y, 0f);

        WorldItem droppedItem = Instantiate(_worldItemPrefab, spawnPosition, Quaternion.identity);
        if (droppedItem == null)
            return false;

        droppedItem.Initialize(weaponData);

        if (droppedItem.TryGetComponent<Rigidbody2D>(out var rb))
        {
            Vector2 dropImpulse = new Vector2 (facingDir * _dropImpulseX, _dropImpulseY);
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(dropImpulse, ForceMode2D.Impulse);
        }

        GameObject weaponObjectToDestroy = CurrentWeapon.gameObject;
        UnequipWeapon();
        Destroy(weaponObjectToDestroy);
        
        return true;
    }

    #endregion

    #region Grip Point Alignment Logic (Stage 4.5)

    /// <summary>
    /// Menyelaraskan posisi dan rotasi root senjata agar Grip Point tepat berimpit dengan holder tangan
    /// </summary>
    private void AlignWeaponGrip(Transform weaponRoot, string gripName)
    {
        Transform gripPoint = FindGripPoint(weaponRoot, gripName);

        if (gripPoint != null)
        {
            // 1. Hitung rotasi lokal terbalik dari Grip Point
            Quaternion gripLocalRot = gripPoint.localRotation;
            Quaternion inverseGripRot = Quaternion.Inverse(gripLocalRot);
            weaponRoot.localRotation = inverseGripRot;

            // 2. Geser root senjata agar posisi Grip Point tepat berada di (0, 0, 0) holder
            Vector3 gripLocalPos = gripPoint.localPosition;
            weaponRoot.localPosition = -(inverseGripRot * gripLocalPos);
        }
        else
        {
            // FALLBACK: Jika Grip Point tidak ditemukan, gunakan pivot default (0,0,0)
            Debug.LogWarning($"<color=yellow>[WeaponEquipmentManager]</color> Grip Point '{gripName}' tidak ditemukan pada '{weaponRoot.name}'. Menggunakan fallback pivot default.", this);
            weaponRoot.localPosition = Vector3.zero;
            weaponRoot.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Mencari Transform Grip Point di dalam hierarki prefab senjata
    /// </summary>
    private Transform FindGripPoint(Transform weaponRoot, string gripName)
    {
        // Cari child langsung terlebih dahulu (O(1) search)
        Transform directChild = weaponRoot.Find(gripName);
        if (directChild != null) return directChild;

        // Fallback pencarian rekursif jika Grip Point diletakkan di dalam sub-child
        Transform[] allChildren = weaponRoot.GetComponentsInChildren<Transform>();
        for (int i = 0; i < allChildren.Length; i++)
        {
            if (allChildren[i].name == gripName)
                return allChildren[i];
        }

        return null;
    }

    #endregion
}