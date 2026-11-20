using UnityEngine;

public class GunLogic : WeaponLogic
{
    [Header("Transform Referensi Senjata Api")]
    [Tooltip("Titik keluarnya peluru / muzzle flash")]
    [SerializeField] private Transform _firePoint;

    public Transform FirePoint => _firePoint != null ? _firePoint : transform;

    public override void TriggerAttack(GameObject owner)
    {
        if (_weaponData != null && !IsAttacking)
        {
            _currentOwner = owner;
            _weaponData.Activate(owner, this);
        }
    }

    public virtual void PerformShoot(GameObject owner, GunWeaponSO data)
    {
        Debug.Log($"<color=orange>[GunLogic]</color> '{data.WeaponName}' ditembakkan oleh {owner.name}!", this);
    }
}