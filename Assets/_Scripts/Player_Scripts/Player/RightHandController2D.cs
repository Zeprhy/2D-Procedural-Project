using UnityEngine;

/// <summary>
/// Right hand controller - handles input, weapon binding, and delegates procedural animation to ProceduralArm2DRebuild.
/// </summary>
[DisallowMultipleComponent]
public class RightHandController2D : MonoBehaviour
{
    #region Inspector References

    [Header("Input")]
    [SerializeField] private PlayerAimTracker2D _aimTracker;

    [Header("Procedural Arm")]
    [SerializeField] private ProceduralArm2DRebuild _proceduralArm;

    [Header("Weapon")]
    [SerializeField] private WeaponLogic _equippedWeapon;

    #endregion

    #region Public Properties

    public WeaponLogic EquippedWeapon => _equippedWeapon;

    /// <summary>Character facing direction (1 = right, -1 = left).</summary>
    public float FacingDirection => _proceduralArm != null ? _proceduralArm.FacingDirection : 1f;

    /// <summary>Current aim direction in world space.</summary>
    public Vector2 AimDirection => _proceduralArm != null ? _proceduralArm.AimDirection : Vector2.right;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_aimTracker == null) _aimTracker = GetComponent<PlayerAimTracker2D>();
        if (_proceduralArm == null) _proceduralArm = GetComponent<ProceduralArm2DRebuild>();
    }

    private void LateUpdate()
    {
        if (_aimTracker == null || _proceduralArm == null) return;

        // Update aim target from aim tracker
        _proceduralArm.SetAimTarget(_aimTracker.MouseWorldPosition, _aimTracker.IsAimingRight);

        GunLogic gun = _equippedWeapon as GunLogic;

        // Process trigger input (tembakan terjadi di sini)
        ProcessRightTriggerInput();

        // Konsumsi impulse recoil satu kali per tembakan, bukan mengirim CurrentAimRecoil setiap frame.
        if (gun != null)
        {
            float recoilImpulse = gun.ConsumeArmRecoilImpulse();
            if (recoilImpulse > 0.0001f)
            {
                _proceduralArm.AddRecoil(recoilImpulse);
            }
        }
    }

    #endregion

    #region Weapon Input Handling

    private void ProcessRightTriggerInput()
    {
        if (_equippedWeapon == null) return;

        BaseWeaponSO weaponData = _equippedWeapon.WeaponData;
        WeaponTriggerMode triggerMode = (weaponData != null) ? weaponData.TriggerMode : WeaponTriggerMode.PRESSONLY;

        if (triggerMode == WeaponTriggerMode.PRESSONLY)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _equippedWeapon.TriggerAttack(gameObject);
            }
        }
        else if (triggerMode == WeaponTriggerMode.HOLDANDRELEASE)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _equippedWeapon.OnTriggerDown(gameObject);
            }
            else if (Input.GetMouseButton(0))
            {
                _equippedWeapon.OnTriggerHeld(gameObject);
            }

            if (Input.GetMouseButtonUp(0))
            {
                _equippedWeapon.OnTriggerUp(gameObject);
            }
        }
    }

    #endregion

    #region Weapon Binding API

    /// <summary>Equips a weapon to this hand.</summary>
    public void SetEquippedWeapon(WeaponLogic weapon)
    {
        _equippedWeapon = weapon;
        
        // Apply weapon-specific arm config
        if (_proceduralArm != null)
        {
            _proceduralArm.ApplyWeaponConfig(weapon);
        }
    }

    /// <summary>Unequips current weapon.</summary>
    public void ClearEquippedWeapon()
    {
        _equippedWeapon = null;
        
        // Reset to default arm config
        if (_proceduralArm != null)
        {
            _proceduralArm.ResetToDefaultConfig();
        }
    }

    #endregion
}