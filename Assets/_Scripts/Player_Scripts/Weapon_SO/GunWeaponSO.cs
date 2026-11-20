using UnityEngine;

[CreateAssetMenu(fileName = "New_GunWeapon", menuName = "Weapons/Gun Weapon")]
public class GunWeaponSO : BaseWeaponSO
{
    [Header("Statistik Senjata Api")]
    [SerializeField] private float _damagePerShot = 18f;
    [SerializeField] private float _fireRate = 0.25f;
    [SerializeField] private int _magazineCapacity = 12;
    [SerializeField] private float _reloadDuration = 1.2f;

    [Header("Prefab Proyektil (Opsional untuk Tahap Depan)")]
    [SerializeField] private GameObject _projectilePrefab;

    public float DamagePerShot => _damagePerShot;
    public float FireRate => _fireRate;
    public int MagazineCapacity => _magazineCapacity;
    public float ReloadDuration => _reloadDuration;
    public GameObject ProjectilePrefab => _projectilePrefab;

    public override void Activate(GameObject owner, WeaponLogic weaponLogic)
    {
        if (weaponLogic is GunLogic gunLogic)
            gunLogic.PerformShoot(owner, this);
         else
            Debug.LogWarning($"<color=yellow>[GunWeaponSO]</color> '{WeaponName}' diaktifkan dengan WeaponLogic yang bukan GunLogic!", weaponLogic);
    }
}