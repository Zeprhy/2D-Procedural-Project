using UnityEngine;

public class WeaponEquipmentManager : MonoBehaviour
{
    [Header("Referensi Tangan Kanan")]
    [SerializeField] private RightHandController2D _rightHandController;
    [Tooltip("Titik pasang visual senjata kanan (B_R_Hand -> Item_Holder_R)")]
    [SerializeField] private Transform _rightHandHolder;

    [Header("Senjata Awal (Eksisting / Default)")]
    [Tooltip("WeaponLogic pedang yang sedang digunakan saat ini")]
    [SerializeField] private WeaponLogic _initialWeapon;

    // Properti publik untuk status equipment
    public WeaponLogic CurrentWeapon { get; private set; }
    public bool HasEquippedWeapon => CurrentWeapon != null;

    private void Awake()
    {
        if (_rightHandController == null)
            _rightHandController = GetComponentInChildren<RightHandController2D>();
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

    #region Loot Integration API (Stage 4 & Stage 4.5)

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

        // STAGE 4.5: Lakukan Grip Point Alignment sebelum mengikat logic
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