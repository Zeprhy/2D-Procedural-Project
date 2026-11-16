using UnityEngine;

[CreateAssetMenu(fileName = "New_MeleeWeapon", menuName = "Weapons/Melee Weapon")]
public class MeleeWeaponSO : BaseWeaponSO
{
    [Header("Statistik Melee")]
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _attackCooldown = 0.4f;
    [SerializeField] private float _slashArcAngle = 110f;

    public float Damage => _damage;
    public float AttackCooldown => _attackCooldown;
    public float SlashArcAngle => _slashArcAngle;

    public override void Activate(GameObject owner, WeaponLogic weaponLogic)
    {
        // weaponLogic.PerformMeleeSlash(this);
    }
}