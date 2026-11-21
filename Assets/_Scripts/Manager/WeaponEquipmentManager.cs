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

    public void UnequipWeapon()
    {
        CurrentWeapon = null;
        CurrentWeaponData = null;

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

        if (weaponData.VisualConfiguration == WeaponVisualConfiguration.SinglePrefab)
        {
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

        if (weaponData.VisualConfiguration == WeaponVisualConfiguration.DualPrefab)
        {
            if (weaponData.WeaponPrefab == null || weaponData.SecondaryWeaponPrefab == null)
            {
                Debug.LogWarning($"<color=yellow>[WeaponEquipmentManager]</color> CanEquip ditolak: Salah satu prefab pada Dual Weapon '{weaponData.WeaponName}' belum diisi!", this);
                return false;
            }

            if (_rightHandHolder == null || _leftHandHolder == null)
            {
                Debug.LogError("<color=red>[WeaponEquipmentManager]</color> CanEquip ditolak: Right Hand Holder atau Left Hand Holder belum di-assign di Inspector!", this);
                return false;
            }

            if (CurrentWeapon != null || CurrentSecondaryWeapon != null)
            {
                Debug.Log("<color=white>[WeaponEquipmentManager]</color> Dual Pistol membutuhkan kedua tangan kosong. Lepaskan senjata saat ini terlebih dahulu.", this);
                return false;
            }

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
            return LootSinglePrefabWeapon(worldItem, weaponData);

        if (weaponData.VisualConfiguration == WeaponVisualConfiguration.DualPrefab)
            return LootDualPrefabWeapon(worldItem, weaponData);

        return false;
    }

    private bool LootSinglePrefabWeapon(WorldItem worldItem, BaseWeaponSO weaponData)
    {
         // Instansiasi WeaponPrefab ke dalam Item_Holder_R
        GameObject weaponInstance = Instantiate(weaponData.WeaponPrefab, _rightHandHolder);
        weaponInstance.transform.localScale = Vector3.one;
        AlignWeaponGrip(weaponInstance.transform, "Grip_Point_R");

        // Dapatkan WeaponLogic
        WeaponLogic newWeaponLogic = weaponInstance.GetComponent<WeaponLogic>();
        if (newWeaponLogic == null)
            newWeaponLogic = weaponInstance.GetComponentInChildren<WeaponLogic>();

        if (newWeaponLogic == null)
        {
            Debug.LogError($"<color=red>[WeaponEquipmentManager]</color> WeaponPrefab '{weaponData.WeaponPrefab.name}' tidak memiliki komponen WeaponLogic!", this);
            Destroy(weaponInstance);
            return false;
        }

        // Pasang ke tangan
        EquipExistingWeapon(newWeaponLogic);

        worldItem.MarkAsConsumed();
        Destroy(worldItem.gameObject);

        Debug.Log($"<color=green>[WeaponEquipmentManager]</color> Berhasil mengambil '{weaponData.WeaponName}'!", this);
        return true;
    }

    private bool LootDualPrefabWeapon(WorldItem worldItem, BaseWeaponSO weaponData)
    {
        // PRE-VALIDATION TUNTAS (FAIL-FAST TANPA EXCEPTION)
        if (weaponData.WeaponPrefab == null || weaponData.SecondaryWeaponPrefab == null)
        {
            Debug.LogError($"<color=red>[WeaponEquipmentManager]</color> Loot ditolak: Prefab kanan atau kiri pada '{weaponData.WeaponName}' null!", this);
            return false;
        }

        if (_rightHandHolder == null || _leftHandHolder == null)
        {
            Debug.LogError("<color=red>[WeaponEquipmentManager]</color> Loot ditolak: RightHandHolder atau LeftHandHolder belum di-assign di Inspector!", this);
            return false;
        }

        if (_rightHandController == null || _offHandController == null)
        {
            Debug.LogError("<color=red>[WeaponEquipmentManager]</color> Loot ditolak: RightHandController atau OffHandController belum di-assign di Inspector!", this);
            return false;
        }

        GunWeaponSO gunSO = weaponData as GunWeaponSO;
        if (gunSO != null && _energyCore == null)
        {
            Debug.LogError("<color=red>[WeaponEquipmentManager]</color> Loot ditolak: Senjata adalah GunWeaponSO tetapi GunEnergyCore tidak ditemukan pada Player!", this);
            return false;
        }

        //  REKAM SNAPSHOT KONDISI AWAL (STATE BASELINE)
        WeaponLogic prevRight = CurrentWeapon;
        WeaponLogic prevLeft = CurrentSecondaryWeapon;
        BaseWeaponSO prevData = CurrentWeaponData;

        // Variabel penampung transaksi
        GameObject rightInstance = null;
        GameObject leftInstance = null;
        bool isRightRegistered = false;
        bool isLeftRegistered = false;
        bool isCoreInitialized = false;

        //  TRANSAKSI ATOMIK DENGAN PERLINDUNGAN ROLLBACK
        try
        {
            //right Gun
            rightInstance = Instantiate(weaponData.WeaponPrefab, _rightHandHolder);
            rightInstance.transform.localScale = Vector3.one;
            AlignWeaponGrip(rightInstance.transform, "Grip_Point_R");

            GunLogic rightGun = rightInstance.GetComponent<GunLogic>();
            if (rightGun == null) rightGun = rightInstance.GetComponentInChildren<GunLogic>();

            if (rightGun == null)
            {
                Debug.LogError($"<color=red>[WeaponEquipmentManager]</color> Prefab kanan '{weaponData.WeaponPrefab.name}' tidak memiliki komponen GunLogic!", this);
                RollbackDualEquip(rightInstance, leftInstance, isRightRegistered, isLeftRegistered, isCoreInitialized, prevRight, prevLeft, prevData);
                return false;
            }
            //Left Gun
            leftInstance = Instantiate(weaponData.SecondaryWeaponPrefab, _leftHandHolder);
            leftInstance.transform.localScale = Vector3.one;
            AlignWeaponGrip(leftInstance.transform, "Grip_Point_L");

            GunLogic leftGun = leftInstance.GetComponent<GunLogic>();
            if (leftGun == null) leftGun = leftInstance.GetComponentInChildren<GunLogic>();

            if (leftGun == null)
            {
                Debug.LogError($"<color=red>[WeaponEquipmentManager]</color> Prefab kiri '{weaponData.SecondaryWeaponPrefab.name}' tidak memiliki komponen GunLogic!", this);
                RollbackDualEquip(rightInstance, leftInstance, isRightRegistered, isLeftRegistered, isCoreInitialized, prevRight, prevLeft, prevData);
                return false;
            }

            rightGun.SetHandOrientation(true);
            leftGun.SetHandOrientation(false);

            if (gunSO != null && _energyCore != null)
            {
                _energyCore.InitializeCore(gunSO);
                rightGun.SetEnergyCore(_energyCore);
                leftGun.SetEnergyCore(_energyCore);
                isCoreInitialized = true;
            }

            _rightHandController.SetEquippedWeapon(rightGun);
            isRightRegistered = true;

            _offHandController.SetEquippedWeapon(leftGun);
            isLeftRegistered = true;

            CurrentWeapon = rightGun;
            CurrentSecondaryWeapon = leftGun;
            CurrentWeaponData = weaponData;

            worldItem.MarkAsConsumed();
            Destroy(worldItem.gameObject);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"<color=red>[WeaponEquipmentManager]</color> Exception tak terduga saat transaksi Dual Pistol: {ex.Message}\n{ex.StackTrace}", this);
            RollbackDualEquip(rightInstance, leftInstance, isRightRegistered, isLeftRegistered, isCoreInitialized, prevRight, prevLeft, prevData);
            return false;
        }
    }

    /// <summary>
    /// Mengembalikan seluruh sistem ke kondisi sebelum transaksi dimulai secara aman dan bersih
    /// </summary>
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
        Debug.LogWarning("<color=yellow>[WeaponEquipmentManager]</color> Menjalankan Rollback Transaksi Dual Pistol...", this);

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

        // Hancurkan GameObject visual yang sempat di-instantiate di hierarki
        if (rightInst != null) Destroy(rightInst);
        if (leftInst != null) Destroy(leftInst);

        //  Pulihkan state manager ke snapshot awal
        CurrentWeapon = prevRight;
        CurrentSecondaryWeapon = prevLeft;
        CurrentWeaponData = prevData;
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

        if (CurrentWeapon != null)
            Destroy(CurrentWeapon.gameObject);
        
        if (CurrentSecondaryWeapon != null)
            Destroy(CurrentSecondaryWeapon.gameObject);

        UnequipWeapon();

        Debug.Log($"<color=cyan>[WeaponEquipmentManager]</color> Berhasil menjatuhkan '{weaponData.WeaponName}' ke dunia!", this);
        return true;
    }

    #endregion

    #region Grip Point Alignment Logic

    
    private void AlignWeaponGrip(Transform weaponRoot, string gripName)
    {
        if (weaponRoot == null)
            return;

          Transform holder = weaponRoot.parent;

        if (holder == null)
        {
            Debug.LogWarning(
                "[WeaponEquipmentManager] Weapon tidak memiliki parent Holder!",
                this
            );
            return;
        }

        Transform gripPoint = FindGripPoint(weaponRoot, gripName);

        if (gripPoint == null)
        {
            Debug.LogWarning(
                $"[WeaponEquipmentManager] Grip Point '{gripName}' tidak ditemukan pada '{weaponRoot.name}'. Menggunakan fallback pivot default.",
                this
            );

            weaponRoot.localPosition = Vector3.zero;
            weaponRoot.localRotation = Quaternion.identity;
            return;
        }

        // 1. Hitung orientasi Grip Point relatif terhadap root weapon.
        // Tetap benar meskipun Grip Point berada di dalam child bertingkat.
        Quaternion gripRotationRelativeToRoot =
            Quaternion.Inverse(weaponRoot.rotation) * gripPoint.rotation;

        // 2. Atur rotasi root agar orientasi Grip Point
        // sama dengan orientasi Holder.
        weaponRoot.rotation =
            holder.rotation * Quaternion.Inverse(gripRotationRelativeToRoot);

        // 3. Setelah rotasi berubah, hitung ulang posisi Grip Point.
        // Geser root sampai Grip Point tepat berada di posisi Holder.
        Vector3 positionCorrection =
            holder.position - gripPoint.position;

        weaponRoot.position += positionCorrection;
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