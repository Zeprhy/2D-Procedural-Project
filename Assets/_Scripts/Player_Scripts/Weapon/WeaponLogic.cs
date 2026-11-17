using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(PolygonCollider2D))]
public class WeaponLogic : MonoBehaviour
{
    [Header("Data Senjata")]
    [SerializeField] private BaseWeaponSO _weaponData;

    [Header("Pengaturan Damage & Cooldown")]
    [SerializeField] private float _damageInterval = 0.5f;

    [Header("Target Layer")]
    [SerializeField] private LayerMask _targetLayer;

    private PolygonCollider2D _hitbox;
    private GameObject _currentOwner;
    private bool _isAttacking;

    private Dictionary<Collider2D, float> _hitTargetCooldown = new Dictionary<Collider2D, float>();

    private void Awake()
    {
        _hitbox = GetComponent<PolygonCollider2D>();
        _hitbox.enabled = false;
    }

    public void TriggerAttack(GameObject owner)
    {
        if (_weaponData != null && !_isAttacking)
        {
            _currentOwner = owner;
            _weaponData.Activate(owner, this);
        }
    }

    public void PerformMeleeSlash(GameObject owner, MeleeWeaponSO data) => StartCoroutine(SlashRoutine(data));

    private IEnumerator SlashRoutine(MeleeWeaponSO data)
    {
        _isAttacking = true;
        _hitbox.enabled = true;
        _hitTargetCooldown.Clear();

        yield return new WaitForSeconds(data.AttackCooldown);

        _isAttacking = false;
        _hitbox.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D target) => ProcessDamage(target);
    private void OTriggerStay2D(Collider2D target) => ProcessDamage(target);
    

    private void ProcessDamage(Collider2D target)
    {
        if (target.gameObject == _currentOwner) return;

        if (((1 << target.gameObject.layer) & _targetLayer.value) != 0)
        {
            if (_hitTargetCooldown.TryGetValue(target, out float lastHitTime))
            {
                if (Time.time < lastHitTime + _damageInterval) return;
            }
        }

        _hitTargetCooldown[target] = Time.time;

        float damage = (_weaponData is MeleeWeaponSO melee) ? melee.Damage : 10f;
        Debug.Log($"<color=red>[HIT LAYER]</color> {target.name} (Layer: {LayerMask.LayerToName(target.gameObject.layer)}) menerima {damage} Damage!");
    }
}