using UnityEngine;

public class WeaponEquipmentManager : MonoBehaviour
{
    [Header("Referensi Tangan Kanan")]
    [SerializeField] private RightHandController2D _rightHandController;
    [Tooltip("Titik pasang visual senjata kanan (B_R_Hand -> Item_Holder_R)")]
    [SerializeField] private Transform _rightHandHolder;

    [Header("Referensi Tangan Kiri / Off-Hand")]
    [SerializeField] private OffHandController2D _offHandController;
    [Tooltip("Titik pasang visual senjata kiri (B_L_Hand -> Item_Holder_L)")]
    [SerializeField] private Transform _leftHandHolder;

    [Header("Sistem Senjata Api / Energy Core")]
    [Tooltip("Komponen pengelola resource bersama Dual Pistol")]
    [SerializeField] private GunEnergyCore _energyCore;

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

    public WeaponLogic CurrentWeapon { get; private set; }
    public WeaponLogic CurrentSecondaryWeapon { get; private set; }
    public BaseWeaponSO CurrentWeaponData { get; private set; }

    public bool HasEquippedWeapon => CurrentWeapon != null || CurrentSecondaryWeapon != null;
    public bool IsDualWielding => CurrentWeapon != null && CurrentSecondaryWeapon != null;

    private void Awake()
    {
        if (_rightHandController == null)
            _rightHandController = GetComponentInChildren<RightHandController2D>();

        if (_offHandController == null)
            _offHandController = GetComponentInChildren<OffHandController2D>();

        if (_facingReference == null)
            _facingReference = transform;

        if (_energyCore == null)
            _energyCore = GetComponent<GunEnergyCore>();
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

    /// <summary>
    /// Membersihkan seluruh state equipment, kedua controller tangan, dan mereset GunEnergyCore secara eksplisit
    /// </summary>
    public void UnequipWeapon()
    {
        CurrentWeapon = null;
        CurrentSecondaryWeapon = null;
        CurrentWeaponData = null;

        if (_rightHandController != null)
            _rightHandController.ClearEquippedWeapon();

        if (_offHandController != null)
            _offHandController.ClearEquippedWeapon();

        if (_energyCore != null)
            _energyCore.ResetSystem();
    }

    #region Loot Integration API (Stage 4 & Sub-Stage B2/B4)

    public bool CanEquip(BaseWeaponSO weaponData)
    {
        if (weaponData == null) return false;

        // One-Handed: hanya membutuhkan slot kanan kosong
        if (weaponData.VisualConfiguration == WeaponVisualConfiguration.SinglePrefab)
        {
            if (weaponData.WeaponPrefab == null) return false;
            if (CurrentWeapon != null) return false;
            return true;
        }

        // Two-Handed DualPrefab: mewajibkan kedua slot kosong dan kedua holder tersedia
        if (weaponData.VisualConfiguration == WeaponVisualConfiguration.DualPrefab)
        {
            if (weaponData.WeaponPrefab == null || weaponData.SecondaryWeaponPrefab == null) return false;
            if (_rightHandHolder == null || _leftHandHolder == null) return false;
            if (CurrentWeapon != null || CurrentSecondaryWeapon != null) return false;
            return true;
        }

        return false;
    }

    public bool TryLootWeapon(WorldItem worldItem)
    {
        if (worldItem == null || worldItem.IsConsumed) return false;

        BaseWeaponSO weaponData = worldItem.ItemData;
        if (!CanEquip(weaponData)) return false;

        if (weaponData.VisualConfiguration == WeaponVisualConfiguration.SinglePrefab)
        {
            return LootSinglePrefabWeapon(worldItem, weaponData);
        }

        if (weaponData.VisualConfiguration == WeaponVisualConfiguration.DualPrefab)
        {
            return LootDualPrefabWeapon(worldItem, weaponData);
        }

        return false;
    }

    private bool LootSinglePrefabWeapon(WorldItem worldItem, BaseWeaponSO weaponData)
    {
        GameObject weaponInstance = Instantiate(weaponData.WeaponPrefab, _rightHandHolder);
        weaponInstance.transform.localScale = Vector3.one;
        AlignWeaponGrip(weaponInstance.transform, "Grip_Point_R");

        WeaponLogic newWeaponLogic = weaponInstance.GetComponent<WeaponLogic>();
        if (newWeaponLogic == null)
            newWeaponLogic = weaponInstance.GetComponentInChildren<WeaponLogic>();

        if (newWeaponLogic == null)
        {
            Destroy(weaponInstance);
            return false;
        }

        EquipExistingWeapon(newWeaponLogic);
        worldItem.MarkAsConsumed();
        Destroy(worldItem.gameObject);

        return true;
    }

    private bool LootDualPrefabWeapon(WorldItem worldItem, BaseWeaponSO weaponData)
    {
        if (weaponData.WeaponPrefab == null || weaponData.SecondaryWeaponPrefab == null) return false;
        if (_rightHandHolder == null || _leftHandHolder == null) return false;

        WeaponLogic prevRight = CurrentWeapon;
        WeaponLogic prevLeft = CurrentSecondaryWeapon;
        BaseWeaponSO prevData = CurrentWeaponData;

        GameObject rightInstance = null;
        GameObject leftInstance = null;
        bool isRightRegistered = false;
        bool isLeftRegistered = false;
        bool isCoreInitialized = false;

        try
        {
            // 1. Instansiasi Pistol Kanan
            rightInstance = Instantiate(weaponData.WeaponPrefab, _rightHandHolder);
            rightInstance.transform.localScale = Vector3.one;
            AlignWeaponGrip(rightInstance.transform, "Grip_Point_R");

            GunLogic rightGun = rightInstance.GetComponent<GunLogic>();
            if (rightGun == null) rightGun = rightInstance.GetComponentInChildren<GunLogic>();

            // 2. Instansiasi Pistol Kiri
            leftInstance = Instantiate(weaponData.SecondaryWeaponPrefab, _leftHandHolder);
            leftInstance.transform.localScale = Vector3.one;
            AlignWeaponGrip(leftInstance.transform, "Grip_Point_L");

            GunLogic leftGun = leftInstance.GetComponent<GunLogic>();
            if (leftGun == null) leftGun = leftInstance.GetComponentInChildren<GunLogic>();

            if (rightGun == null || leftGun == null)
            {
                RollbackDualEquip(rightInstance, leftInstance, isRightRegistered, isLeftRegistered, isCoreInitialized, prevRight, prevLeft, prevData);
                return false;
            }

            // 3. Konfigurasi Orientasi Tangan & Shared Energy Core
            GunWeaponSO gunSO = weaponData as GunWeaponSO;
            if (gunSO != null && _energyCore != null)
            {
                _energyCore.InitializeCore(gunSO);
                rightGun.InitializeGun(_energyCore, true);
                leftGun.InitializeGun(_energyCore, false);
                isCoreInitialized = true;
            }

            // 4. Registrasi ke Masing-Masing Hand Controller
            _rightHandController.SetEquippedWeapon(rightGun);
            isRightRegistered = true;

            _offHandController.SetEquippedWeapon(leftGun);
            isLeftRegistered = true;

            // 5. Update State Manager
            CurrentWeapon = rightGun;
            CurrentSecondaryWeapon = leftGun;
            CurrentWeaponData = weaponData;

            // 6. Konsumsi Tepat 1 Objek Dunia
            worldItem.MarkAsConsumed();
            Destroy(worldItem.gameObject);

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"<color=red>[WeaponEquipmentManager]</color> Exception saat Dual Equip: {ex.Message}", this);
            RollbackDualEquip(rightInstance, leftInstance, isRightRegistered, isLeftRegistered, isCoreInitialized, prevRight, prevLeft, prevData);
            return false;
        }
    }

    private void RollbackDualEquip(
        GameObject rightInst, 
        GameObject leftInst, 
        bool rightReg, 
        bool leftReg, 
        bool coreInit, 
        WeaponLogic prevRight, 
        WeaponLogic prevLeft, 
        BaseWeaponSO prevData)
    {
        if (rightReg && _rightHandController != null)
        {
            if (prevRight != null) _rightHandController.SetEquippedWeapon(prevRight);
            else _rightHandController.ClearEquippedWeapon();
        }

        if (leftReg && _offHandController != null)
        {
            if (prevLeft != null) _offHandController.SetEquippedWeapon(prevLeft);
            else _offHandController.ClearEquippedWeapon();
        }

        if (coreInit && _energyCore != null)
            _energyCore.ResetSystem();

        if (rightInst != null) Destroy(rightInst);
        if (leftInst != null) Destroy(leftInst);

        CurrentWeapon = prevRight;
        CurrentSecondaryWeapon = prevLeft;
        CurrentWeaponData = prevData;
    }

    #endregion

    #region Drop System API (Stage 5 & Sub-Stage B4 Atomic Drop)

    /// <summary>
    /// Menjatuhkan seluruh equipment set yang sedang aktif ke dunia game sebagai 1 WorldItem
    /// </summary>
    public bool DropWeapon()
    {
        if (!HasEquippedWeapon) return false;

        // Ambil BaseWeaponSO dari senjata aktif
        BaseWeaponSO weaponData = CurrentWeaponData;
        if (weaponData == null && CurrentWeapon != null)
            weaponData = CurrentWeapon.WeaponData;

        if (weaponData == null || _worldItemPrefab == null)
        {
            Debug.LogError("<color=red>[WeaponEquipmentManager]</color> Drop dibatalkan: Data senjata atau WorldItem prefab null!", this);
            return false;
        }

        // 1. Tentukan posisi dan spawn tepat 1 PF_WorldItem
        float facingDir = _facingReference != null ? Mathf.Sign(_facingReference.lossyScale.x) : Mathf.Sign(transform.lossyScale.x);
        Vector3 spawnPosition = transform.position + new Vector3(_dropOffset.x * facingDir, _dropOffset.y, 0f);

        WorldItem droppedItem = Instantiate(_worldItemPrefab, spawnPosition, Quaternion.identity);
        if (droppedItem == null)
        {
            Debug.LogError("<color=red>[WeaponEquipmentManager]</color> Drop gagal: Gagal instantiate WorldItem!", this);
            return false;
        }

        droppedItem.Initialize(weaponData);

        if (droppedItem.TryGetComponent<Rigidbody2D>(out var rb))
        {
            Vector2 dropImpulse = new Vector2(facingDir * _dropImpulseX, _dropImpulseY);
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(dropImpulse, ForceMode2D.Impulse);
        }

        // 2. Cache referensi GameObject visual sebelum state dikosongkan
        GameObject rightVisualToDestroy = CurrentWeapon != null ? CurrentWeapon.gameObject : null;
        GameObject leftVisualToDestroy = CurrentSecondaryWeapon != null ? CurrentSecondaryWeapon.gameObject : null;

        // 3. Bersihkan seluruh state, controller tangan, dan core secara atomik
        UnequipWeapon();

        // 4. Hancurkan objek visual fisik dengan aman
        if (rightVisualToDestroy != null) Destroy(rightVisualToDestroy);
        if (leftVisualToDestroy != null) Destroy(leftVisualToDestroy);

        Debug.Log($"<color=cyan>[WeaponEquipmentManager]</color> Berhasil menjatuhkan set '{weaponData.WeaponName}' ke dunia!", this);
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