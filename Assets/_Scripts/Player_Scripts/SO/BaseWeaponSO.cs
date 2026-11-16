using System;
using UnityEngine;

[Serializable]
public abstract class BaseWeaponSO : ScriptableObject
{
    [Header("Informasi Dasar")]
    [SerializeField] private string _weaponID;
    [SerializeField] private string _weaponName;
    [SerializeField] private Sprite _weaponIcon;

    public string WeaponID => _weaponID;
    public string WeaponName => _weaponName;
    public Sprite WeaponIcon => _weaponIcon;

    // Do Something !!
    public abstract void Activate(GameObject owner, WeaponLogic weaponLogic);
}