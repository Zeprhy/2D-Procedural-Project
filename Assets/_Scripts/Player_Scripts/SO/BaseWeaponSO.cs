using System;
using UnityEngine;

[Serializable]
public abstract class BaseWeaponSO : ScriptableObject
{
    [Header("Informasi Dasar")]
    [SerializeField] private string _weaponID;
    [SerializeField] private string _weaponName;
    [SerializeField] private Sprite _weaponIcon;

    [Header("Fisika Bobot Senjata")]
    [SerializeField] private float _weight = 6f;

    [Header("Kategori Weapon")]
    [SerializeField] private WeaponHandType weaponHandType;

    [Header("Konfigurasi Visual & Prefab")]
    [Tooltip("Prefab visual senjata saat dipasang di tangan karakter (memiliki WeaponLogic & Hitbox)")]
    [SerializeField] private GameObject _weaponPrefab;
    [Tooltip("Sprite khusus saat senjata tergeletak di tanah (jika kosong, otomatis fallback ke WeaponIcon)")]
    [SerializeField] private Sprite _worldItemSprite;

    public string WeaponID => _weaponID;
    public string WeaponName => _weaponName;
    public Sprite WeaponIcon => _weaponIcon;
    public float Weight => _weight;
    public WeaponHandType HandType => weaponHandType;

    public GameObject WeaponPrefab => _weaponPrefab;
    
    public Sprite WorldItemSprite => _worldItemSprite != null ? _worldItemSprite : _weaponIcon;

    public abstract void Activate(GameObject owner, WeaponLogic weaponLogic);
}

public enum WeaponHandType
{
    OneHanded,
    TwoHanded
}