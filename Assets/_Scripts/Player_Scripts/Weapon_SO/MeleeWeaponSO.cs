using UnityEngine;

[CreateAssetMenu(fileName = "New_MeleeWeapon", menuName = "Weapons/Melee Weapon")]
public class MeleeWeaponSO : BaseWeaponSO
{
    [Header("Statistik Melee")]
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _attackCooldown = 0.4f;
    [SerializeField] private float _slashArcAngle = 110f;

    [Header("SFX Melee")]
    [Tooltip("Suara ayunan/slash saat menyerang")]
    [SerializeField] private AudioClip _swingClip;
    [Tooltip("Suara hit normal ke musuh")]
    [SerializeField] private AudioClip _hitNormalClip;
    [Tooltip("Suara hit critical (headshot/overhead)")]
    [SerializeField] private AudioClip _hitCriticalClip;
    [Tooltip("Variasi pitch random untuk swing (±value)")]
    [Range(0f, 0.5f)] [SerializeField] private float _swingPitchVariation = 0.1f;
    [Tooltip("Variasi pitch random untuk hit (±value)")]
    [Range(0f, 0.5f)] [SerializeField] private float _hitPitchVariation = 0.15f;

    public float Damage => _damage;
    public float AttackCooldown => _attackCooldown;
    public float SlashArcAngle => _slashArcAngle;

    // SFX Getters
    public AudioClip SwingClip => _swingClip;
    public AudioClip HitNormalClip => _hitNormalClip;
    public AudioClip HitCriticalClip => _hitCriticalClip;
    public float SwingPitchVariation => _swingPitchVariation;
    public float HitPitchVariation => _hitPitchVariation;

    public override void Activate(GameObject owner, WeaponLogic weaponLogic)
    {
        if (weaponLogic is MeleeLogic meleeLogic)
        {
            meleeLogic.PerformMeleeSlash(owner, this);
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[MeleeWeaponSO]</color> '{WeaponName}' diaktifkan dengan WeaponLogic yang bukan MeleeLogic!", weaponLogic);
        }
    }
}