using System;
using UnityEngine;

[Serializable]
public abstract class BaseWeaponSO : ScriptableObject
{
    [Header("Informasi Dasar")]
    [SerializeField] private string _weaponID;
    [SerializeField] private string _weaponName;
    [SerializeField] private Sprite _weaponIcon;

    [Header ("Fisika Bobot Senjata")]
    [SerializeField] private float _weight = 6f;

    [Header ("Kategori Weapon")]
    [SerializeField] private WeaponHandType weaponHandType;

    public string WeaponID => _weaponID;
    public string WeaponName => _weaponName;
    public Sprite WeaponIcon => _weaponIcon;
    public float Weight => _weight;

    // Do Something !!
    public abstract void Activate(GameObject owner, WeaponLogic weaponLogic);
}

public enum WeaponHandType
{
    OneHanded,
    TwoHanded
}