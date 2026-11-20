using UnityEngine;

public class GunLogic : WeaponLogic
{
    [Header("Transform Referensi Senjata Api")]
    [Tooltip("Titik keluarnya peluru / muzzle flash")]
    [SerializeField] private Transform _firePoint;

    [Header("Sistem Energi Bersama (Stage 6.2)")]
    [Tooltip("Referensi GunEnergyCore bersama yang disuntikkan oleh WeaponEquipmentManager")]
    [SerializeField] protected GunEnergyCore _energyCore;

    public Transform FirePoint => _firePoint != null ? _firePoint : transform;
    public GunEnergyCore EnergyCore => _energyCore;


    /// <summary>
    /// Dipanggil oleh WeaponEquipmentManager saat senjata dipasang ke tangan
    /// </summary>
    public void SetEnergyCore(GunEnergyCore core) => _energyCore = core;
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