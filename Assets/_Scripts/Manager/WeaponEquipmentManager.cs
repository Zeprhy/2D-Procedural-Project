using UnityEngine;

public class WeaponEquipmentManager : MonoBehaviour
{
    [Header("Referensi Tangan Kanan")]
    [SerializeField] private RightHandController2D _rightHandController;
    [Tooltip("Titik pasang visual senjata kanan (B_R_Hand -> Item_Holder_R)")]
    [SerializeField] private Transform _rightHandHolder;

    [Header("Senjata Awal (Eksisting / Default)")]
    [Tooltip("WeaponLogic pedang yang sedang digunakan saat ini (untuk transisi aman)")]
    [SerializeField] private WeaponLogic _initialWeapon;

    public WeaponLogic CurrentWeapon { get; private set; }
    public bool HasEquippedWeapon => CurrentWeapon != null;

    private void Awake()
    {
        if (_rightHandController == null)
            _rightHandController = GetComponentInChildren<RightHandController2D>();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterWeaponEquipmentManager(this);
        else
            Debug.LogError("<color=red>[WeaponEquipmentManager]</color> GameManager.Instance tidak ditemukan di Scene! Pastikan ada GameObject dengan komponen GameManager.", this);


        if (_initialWeapon != null)
            EquipExistingWeapon(_initialWeapon);
        else if (_rightHandController != null && _rightHandController.EquippedWeapon != null)
            EquipExistingWeapon(_rightHandController.EquippedWeapon);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.UnregisterWeaponEquipmentManager(this);
    }

    /// <summary>
    /// Memasang senjata runtime yang sudah ada ke slot tangan kanan
    /// </summary>
    public void EquipExistingWeapon(WeaponLogic weapon)
    {
        CurrentWeapon = weapon;

        if (_rightHandController != null)
        {
            _rightHandController.SetEquippedWeapon(weapon);
        }
    }

    public void UnequipWeapon()
    {
        CurrentWeapon = null;

        if (_rightHandController != null)
            _rightHandController.ClearEquippedWeapon();
    }
}