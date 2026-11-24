using System;
using UnityEngine;

public enum WeaponHandType
{
    OneHanded,
    TwoHanded
}

public enum WeaponVisualConfiguration
{
    SinglePrefab,
    DualPrefab
}

[Serializable]
public abstract class BaseWeaponSO : ScriptableObject
{
    [Header("Informasi Dasar")]
    [SerializeField] private string _weaponID;
    [SerializeField] private string _weaponName;
    [SerializeField] private Sprite _weaponIcon;

    [Header("Fisika Bobot Senjata")]
    [SerializeField] protected float _weight = 6f;

    [Header("Kategori Slot Tangan")]
    [Tooltip("Menentukan apakah senjata menggunakan satu tangan atau mengunci kedua tangan")]
    [SerializeField] private WeaponHandType weaponHandType = WeaponHandType.OneHanded;

    [Header("Mode Input Pelatuk (Trigger Mode)")]
    [Tooltip("PressOnly untuk Melee (1 klik = 1 tebas), HoldAndRelease untuk Senjata Api")]
    [SerializeField] protected WeaponTriggerMode _triggerMode = WeaponTriggerMode.PRESSONLY;

    [Header("Konfigurasi Visual & Prefab")]
    [Tooltip("Menentukan apakah equipment set menggunakan 1 prefab visual atau 2 prefab terpisah")]
    [SerializeField] private WeaponVisualConfiguration _visualConfiguration = WeaponVisualConfiguration.SinglePrefab;

    [Tooltip("Prefab visual senjata utama (Tangan Kanan)")]
    [SerializeField] private GameObject _weaponPrefab;

    [Tooltip("Prefab visual senjata kedua (Khusus DualPrefab / Tangan Kiri). Kosongkan jika SinglePrefab.")]
    [SerializeField] private GameObject _secondaryWeaponPrefab;

    [Tooltip("Sprite khusus saat senjata tergeletak di tanah (jika kosong, otomatis fallback ke WeaponIcon)")]
    [SerializeField] private Sprite _worldItemSprite;

    [Header("Procedural Arm Overrides (Optional)")]
    [Tooltip("Aktifkan untuk override konfigurasi lengan prosedural saat senjata ini diequip")]
    [SerializeField] private bool _overrideArmConfig = false;

    [SerializeField, Tooltip("Jangkauan minimum lengan")] private float _minReach = 0.2f;
    [SerializeField, Tooltip("Jangkauan maksimum lengan")] private float _maxReach = 0.58f;
    [SerializeField, Tooltip("Kekuatan pegas jangkauan")] private float _springStiffness = 130f;
    [SerializeField, Tooltip("Redaman pegas jangkauan")] private float _springDamping = 14f;
    [SerializeField, Tooltip("Sudut bidik minimum (derajat)")] private float _minAimAngle = -55f;
    [SerializeField, Tooltip("Sudut bidik maksimum (derajat)")] private float _maxAimAngle = 70f;
    [SerializeField, Tooltip("Waktu lag bidik (detik)")] private float _aimLagTime = 0.12f;
    [SerializeField, Tooltip("Offset sudut tangan (derajat)")] private float _handAngleOffset = -20f;
    [SerializeField, Tooltip("Offset posisi idle tangan")] private Vector2 _idleOffset = new(0.12f, -0.35f);
    [SerializeField, Tooltip("Sudut idle tangan (derajat)")] private float _idleAngle = -65f;

    // Public Getters
    public string WeaponID => _weaponID;
    public string WeaponName => _weaponName;
    public Sprite WeaponIcon => _weaponIcon;
    public float Weight => _weight;
    public WeaponHandType HandType => weaponHandType;
    public WeaponTriggerMode TriggerMode => _triggerMode;
    public WeaponVisualConfiguration VisualConfiguration => _visualConfiguration;

    public GameObject WeaponPrefab => _weaponPrefab;
    public GameObject SecondaryWeaponPrefab => _secondaryWeaponPrefab;
    public bool HasSecondaryVisual => _secondaryWeaponPrefab != null && _visualConfiguration == WeaponVisualConfiguration.DualPrefab;

    public Sprite WorldItemSprite => _worldItemSprite != null ? _worldItemSprite : _weaponIcon;

    // Arm Config Getters (only valid if _overrideArmConfig is true)
    public bool OverrideArmConfig => _overrideArmConfig;
    public float MinReach => _minReach;
    public float MaxReach => _maxReach;
    public float SpringStiffness => _springStiffness;
    public float SpringDamping => _springDamping;
    public float MinAimAngle => _minAimAngle;
    public float MaxAimAngle => _maxAimAngle;
    public float AimLagTime => _aimLagTime;
    public float HandAngleOffset => _handAngleOffset;
    public Vector2 IdleOffset => _idleOffset;
    public float IdleAngle => _idleAngle;

    public abstract void Activate(GameObject owner, WeaponLogic weaponLogic);
}