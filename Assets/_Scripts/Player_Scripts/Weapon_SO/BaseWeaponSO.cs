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
    [SerializeField] private float _weight = 6f;

    [Header("Kategori Slot Tangan")]
    [Tooltip("Menentukan apakah senjata menggunakan satu tangan atau mengunci kedua tangan")]
    [SerializeField] private WeaponHandType weaponHandType = WeaponHandType.OneHanded;

    [Header("Konfigurasi Visual & Prefab")]
    [Tooltip("Menentukan apakah equipment set menggunakan 1 prefab visual atau 2 prefab terpisah")]
    [SerializeField] private WeaponVisualConfiguration _visualConfiguration = WeaponVisualConfiguration.SinglePrefab;

    [Tooltip("Prefab visual senjata utama (Tangan Kanan)")]
    [SerializeField] private GameObject _weaponPrefab;

    [Tooltip("Prefab visual senjata kedua (Khusus DualPrefab / Tangan Kiri). Kosongkan jika SinglePrefab.")]
    [SerializeField] private GameObject _secondaryWeaponPrefab;

    [Tooltip("Sprite khusus saat senjata tergeletak di tanah (jika kosong, otomatis fallback ke WeaponIcon)")]
    [SerializeField] private Sprite _worldItemSprite;

    // Public Getters (Preserving Existing Serialized Data)
    public string WeaponID => _weaponID;
    public string WeaponName => _weaponName;
    public Sprite WeaponIcon => _weaponIcon;
    public float Weight => _weight;
    public WeaponHandType HandType => weaponHandType;
    public WeaponVisualConfiguration VisualConfiguration => _visualConfiguration;

    public GameObject WeaponPrefab => _weaponPrefab;
    public GameObject SecondaryWeaponPrefab => _secondaryWeaponPrefab;
    public bool HasSecondaryVisual => _secondaryWeaponPrefab != null && _visualConfiguration == WeaponVisualConfiguration.DualPrefab;

    public Sprite WorldItemSprite => _worldItemSprite != null ? _worldItemSprite : _weaponIcon;

    public abstract void Activate(GameObject owner, WeaponLogic weaponLogic);
}