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
    [SerializeField] private float _maxBattery = 100f;
    [SerializeField] private float _batteryCostPerShot = 10f;
    [SerializeField] private float _batteryRegenRate = 25f;

    [Header("Sistem Panas (Heat & Overheat)")]
    [SerializeField] private float _maxHeat = 100f;
    [SerializeField] private float _heatPerShot = 15f;
    [SerializeField] private float _heatDissipationRate = 20f;
    [SerializeField] private float _idleDelay = 1.75f;
    [SerializeField] private float _overheatCooldown = 2.5f;
    [Range(0.1f, 0.8f)]
    [SerializeField] private float _overheatRecoveryRatio = 0.40f;

    [Header("Sistem Recoil & Player Push")]
    [Tooltip("Jarak dorongan mundur visual senjata (lokal) saat menembak")]
    [SerializeField] private float _weaponRecoilDistance = 0.08f;
    [Tooltip("Sudut rotasi hentakan visual senjata saat menembak (derajat)")]
    [SerializeField] private float _weaponRecoilAngle = 5f;
    [Tooltip("Kecepatan pemulihan visual senjata kembali ke posisi semula")]
    [SerializeField] private float _weaponRecoilRecoverySpeed = 16f;

    [Tooltip("Penambahan sudut recoil bidikan tangan per tembakan")]
    [SerializeField] private float _aimRecoilKick = 4f;
    [Tooltip("Batas maksimum akumulasi recoil bidikan tangan")]
    [SerializeField] private float _maxAimRecoil = 12f;
    [Tooltip("Kecepatan pemulihan sudut bidikan kembali normal")]
    [SerializeField] private float _aimRecoilRecoverySpeed = 18f;

    [Tooltip("Kekuatan dorongan mundur fisik yang diterima pemain saat menembak")]
    [SerializeField] private float _playerPushForce = 2.2f;

    [Header("Prefab Proyektil")]
    [SerializeField] private GameObject _projectilePrefab;

    [Header("SFX Gun")]
    [Tooltip("Suara tembakan (shoot/pew)")]
    [SerializeField] private AudioClip _shootClip;
    [Tooltip("Suara peluru mengenai musuh")]
    [SerializeField] private AudioClip _impactEnemyClip;
    [Tooltip("Suara peluru mengenai dinding/rintangan")]
    [SerializeField] private AudioClip _impactWallClip;
    [Tooltip("Suara klik kosong (baterai habis)")]
    [SerializeField] private AudioClip _emptyClickClip;
    [Tooltip("Suara overheat")]
    [SerializeField] private AudioClip _overheatClip;
    [Tooltip("Variasi pitch random untuk shoot (±value)")]
    [Range(0f, 0.5f)] [SerializeField] private float _shootPitchVariation = 0.1f;
    [Tooltip("Variasi pitch random untuk impact (±value)")]
    [Range(0f, 0.5f)] [SerializeField] private float _impactPitchVariation = 0.15f;

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

    public float WeaponRecoilDistance => _weaponRecoilDistance;
    public float WeaponRecoilAngle => _weaponRecoilAngle;
    public float WeaponRecoilRecoverySpeed => _weaponRecoilRecoverySpeed;
    public float AimRecoilKick => _aimRecoilKick;
    public float MaxAimRecoil => _maxAimRecoil;
    public float AimRecoilRecoverySpeed => _aimRecoilRecoverySpeed;
    public float PlayerPushForce => _playerPushForce;

    public GameObject ProjectilePrefab => _projectilePrefab;

    // SFX Getters
    public AudioClip ShootClip => _shootClip;
    public AudioClip ImpactEnemyClip => _impactEnemyClip;
    public AudioClip ImpactWallClip => _impactWallClip;
    public AudioClip EmptyClickClip => _emptyClickClip;
    public AudioClip OverheatClip => _overheatClip;
    public float ShootPitchVariation => _shootPitchVariation;
    public float ImpactPitchVariation => _impactPitchVariation;

    private void Reset()
    {
        _triggerMode = WeaponTriggerMode.HOLDANDRELEASE;
        _weight = 4f;
    }

    public override void Activate(GameObject owner, WeaponLogic weaponLogic)
    {
        if (weaponLogic is GunLogic gunLogic)
        {
            // gunLogic.PerformShoot(owner, this);
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[GunWeaponSO]</color> '{WeaponName}' diaktifkan dengan WeaponLogic yang bukan GunLogic!", weaponLogic);
        }
    }
}