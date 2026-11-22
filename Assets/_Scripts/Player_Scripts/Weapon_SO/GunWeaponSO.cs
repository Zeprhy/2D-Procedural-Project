using UnityEngine;

[CreateAssetMenu(fileName = "New_GunWeapon", menuName = "Weapons/Gun Weapon")]
public class GunWeaponSO : BaseWeaponSO
{
    [Header("Statistik Tembakan")]
    [SerializeField] private float _damagePerShot = 18f;
    [SerializeField] private float _fireRate = 0.15f;
    [SerializeField] private float _projectileSpeed = 22f;
    [SerializeField] private float _projectileLifetime = 2.5f;

    [Header("Sistem Baterai (Energy System)")]
    [Tooltip("Kapasitas maksimum baterai bersama")]
    [SerializeField] private float _maxBattery = 100f;
    [Tooltip("Biaya baterai per satu kali tembakan")]
    [SerializeField] private float _batteryCostPerShot = 10f;
    [Tooltip("Kecepatan pengisian baterai per detik saat Idle")]
    [SerializeField] private float _batteryRegenRate = 25f;

    [Header("Sistem Panas (Heat & Overheat)")]
    [Tooltip("Kapasitas maksimum panas sebelum Overheat")]
    [SerializeField] private float _maxHeat = 100f;
    [Tooltip("Pertambahan panas per satu kali tembakan")]
    [SerializeField] private float _heatPerShot = 15f;
    [Tooltip("Kecepatan pendinginan panas per detik")]
    [SerializeField] private float _heatDissipationRate = 20f;
    [Tooltip("Waktu hening tanpa aksi (detik) sebelum sistem masuk ke mode Idle")]
    [SerializeField] private float _idleDelay = 1.75f;
    [Tooltip("Durasi minimum penguncian saat terjadi Overheat (detik)")]
    [SerializeField] private float _overheatCooldown = 2.5f;
    [Tooltip("Persentase panas yang harus dicapai agar senjata pulih dari Overheat (0.4 = 40% dari MaxHeat)")]
    [Range(0.1f, 0.8f)]
    [SerializeField] private float _overheatRecoveryRatio = 0.40f;

    [Header("Prefab Proyektil & VFX")]
    [SerializeField] private GameObject _projectilePrefab;

    // Public Getters
    public float DamagePerShot => _damagePerShot;
    public float FireRate => _fireRate;
    public float ProjectileSpeed => _projectileSpeed;
    public float ProjectileLifetime => _projectileLifetime;

    public float MaxBattery => _maxBattery;
    public float BatteryCostPerShot => _batteryCostPerShot;
    public float BatteryRegenRate => _batteryRegenRate;

    public float MaxHeat => _maxHeat;
    public float HeatPerShot => _heatPerShot;
    public float HeatDissipationRate => _heatDissipationRate;
    public float IdleDelay => _idleDelay;
    public float OverheatCooldown => _overheatCooldown;
    public float OverheatRecoveryThreshold => _maxHeat * _overheatRecoveryRatio;

    public GameObject ProjectilePrefab => _projectilePrefab;

    public override void Activate(GameObject owner, WeaponLogic weaponLogic)
    {
        if (weaponLogic is GunLogic)
        {
            // GunLogic menggunakan Trigger Lifecycle:
            // OnTriggerDown -> OnTriggerHeld -> OnTriggerUp.
            // Penembakan dilakukan ketika trigger dilepas.
            // Tidak perlu memanggil method tembak dari sini.
        }
        else
        {
            Debug.LogWarning(
                $"<color=yellow>[GunWeaponSO]</color> '{WeaponName}' diaktifkan dengan WeaponLogic yang bukan GunLogic!",
                weaponLogic
            );
        }
    }
}