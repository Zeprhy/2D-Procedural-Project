using UnityEngine;

public class WeaponEquipmentManager : MonoBehaviour
{
    [Header("Referensi Tangan Kanan")]
    [SerializeField] private RightHandController2D _rightHandController;
    [Tooltip("Titik pasang visual senjata kanan (B_R_Hand -> Item_Holder_R)")]
    [SerializeField] private Transform _rightHandHolder;

    [Header("Referensi Tangan Kiri / Off-Hand (Stage 6.1)")]
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
        CurrentWeaponData = (weapon != null) ? weapon.WeaponData : null;

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
        CurrentWeaponData = null;

        if (_rightHandController != null)
        {
            _rightHandController.ClearEquippedWeapon();
        }
    }

    #region Loot Integration API (Stage 4 & 4.5)

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
            Debug.Log($"<color=white>[WeaponEquipmentManager]</color> Tangan kanan sudah terisi oleh '{CurrentWeapon.name}'. Tekan 'Q' untuk Drop terlebih dahulu.", this);
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

        // 1. Instansiasi WeaponPrefab ke dalam Item_Holder_R
        GameObject weaponInstance = Instantiate(weaponData.WeaponPrefab, _rightHandHolder);
        weaponInstance.transform.localScale = Vector3.one;

        // 2. Alignment Grip Point presisi
        AlignWeaponGrip(weaponInstance.transform, "Grip_Point_R");

        // 3. Dapatkan WeaponLogic
        WeaponLogic newWeaponLogic = weaponInstance.GetComponent<WeaponLogic>();
        if (newWeaponLogic == null)
            newWeaponLogic = weaponInstance.GetComponentInChildren<WeaponLogic>();

        if (newWeaponLogic == null)
        {
            Debug.LogError($"<color=red>[WeaponEquipmentManager]</color> WeaponPrefab '{weaponData.WeaponPrefab.name}' tidak memiliki komponen WeaponLogic!", this);
            Destroy(weaponInstance);
            return false;
        }

        // 4. Pasang ke tangan
        EquipExistingWeapon(newWeaponLogic);

        // 5. Konsumsi dan hancurkan objek WorldItem di lantai
        worldItem.MarkAsConsumed();
        Destroy(worldItem.gameObject);

        Debug.Log($"<color=green>[WeaponEquipmentManager]</color> Berhasil mengambil '{weaponData.WeaponName}'!", this);
        return true;
    }

    #endregion

    #region Drop System API (Stage 5)

    public bool DropWeapon()
    {
        if (!HasEquippedWeapon)
        {
            Debug.Log("<color=white>[WeaponEquipmentManager]</color> Tidak ada senjata yang sedang dipegang untuk dijatuhkan.", this);
            return false;
        }

        BaseWeaponSO weaponData = CurrentWeaponData;
        if (weaponData == null && CurrentWeapon != null)
        {
            weaponData = CurrentWeapon.WeaponData;
        }

        if (weaponData == null)
        {
            Debug.LogError("<color=red>[WeaponEquipmentManager]</color> Drop gagal: Senjata aktif tidak memiliki referensi BaseWeaponSO! Senjata tidak akan dihapus demi keamanan.", this);
            return false;
        }

        if (_worldItemPrefab == null)
        {
            Debug.LogError("<color=red>[WeaponEquipmentManager]</color> Drop gagal: '_worldItemPrefab' belum di-assign pada Inspector WeaponEquipmentManager! Senjata tidak akan dihapus.", this);
            return false;
        }

        float facingDir = _facingReference != null ? Mathf.Sign(_facingReference.lossyScale.x) : Mathf.Sign(transform.lossyScale.x);
        Vector3 spawnPosition = transform.position + new Vector3(_dropOffset.x * facingDir, _dropOffset.y, 0f);

        WorldItem droppedItem = Instantiate(_worldItemPrefab, spawnPosition, Quaternion.identity);
        if (droppedItem == null)
        {
            Debug.LogError("<color=red>[WeaponEquipmentManager]</color> Drop gagal: Gagal menginstansiasi WorldItem prefab!", this);
            return false;
        }

        droppedItem.Initialize(weaponData);

        if (droppedItem.TryGetComponent<Rigidbody2D>(out var rb))
        {
            Vector2 dropImpulse = new Vector2(facingDir * _dropImpulseX, _dropImpulseY);
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(dropImpulse, ForceMode2D.Impulse);
        }

        GameObject weaponObjectToDestroy = CurrentWeapon.gameObject;
        UnequipWeapon();
        Destroy(weaponObjectToDestroy);

        Debug.Log($"<color=cyan>[WeaponEquipmentManager]</color> Berhasil menjatuhkan '{weaponData.WeaponName}' ke dunia!", this);
        return true;
    }

    #endregion

    #region Grip Point Alignment Logic

    private void AlignWeaponGrip(Transform weaponRoot, string gripName)
    {
        Transform gripPoint = FindGripPoint(weaponRoot, gripName);

        if (gripPoint != null)
        {
            Quaternion gripLocalRot = gripPoint.localRotation;
            Quaternion inverseGripRot = Quaternion.Inverse(gripLocalRot);
            weaponRoot.localRotation = inverseGripRot;

            Vector3 gripLocalPos = gripPoint.localPosition;
            weaponRoot.localPosition = -(inverseGripRot * gripLocalPos);
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[WeaponEquipmentManager]</color> Grip Point '{gripName}' tidak ditemukan pada '{weaponRoot.name}'. Menggunakan fallback pivot default.", this);
            weaponRoot.localPosition = Vector3.zero;
            weaponRoot.localRotation = Quaternion.identity;
        }
    }

    private Transform FindGripPoint(Transform weaponRoot, string gripName)
    {
        Transform directChild = weaponRoot.Find(gripName);
        if (directChild != null) return directChild;

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