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
        // Safety check jika GameManager baru siap di frame yang sama
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

    /// <summary>
    /// Memasang senjata ke slot tangan kanan
    /// </summary>
    public void EquipExistingWeapon(WeaponLogic weapon)
    {
        CurrentWeapon = weapon;

        if (_rightHandController != null)
        {
            _rightHandController.SetEquippedWeapon(weapon);
        }
    }

    /// <summary>
    /// Melepaskan senjata dari slot tangan kanan
    /// </summary>
    public void UnequipWeapon()
    {
        CurrentWeapon = null;

        if (_rightHandController != null)
        {
            _rightHandController.ClearEquippedWeapon();
        }
    }
}