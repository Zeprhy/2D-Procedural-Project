using UnityEngine;

public class OffHandController2D : MonoBehaviour
{
    [Header("Referensi Input")]
    [SerializeField] private PlayerAimTracker2D _aimTracker;

    [Header("Referensi Tubuh")]
    [SerializeField] private Transform _leftShoulderPivot;
    [SerializeField] private Transform _gfxTransform;

    [Header("Target & Tulang Tangan Kiri")]
    [SerializeField] private Transform _lHandTarget;
    [SerializeField] private Transform _lHandBone;
    [SerializeField] private float _handAngleOffset = 90f;

    [Header("Senjata yang Sedang Dipegang")]
    [SerializeField] private WeaponLogic _equippedWeapon;

    [Header("Pose Santai (Idle saat Klik Kanan Dilepas)")]
    [SerializeField] private Vector2 _idleOffset = new Vector2(-0.05f, -0.38f);

    [Header("Jangkauan Lengan Kiri (Dynamic Reach)")]
    [SerializeField] private float _minReach = 0.20f;
    [SerializeField] private float _maxReach = 0.50f;
    [SerializeField] private float _minAimAngle = -60f;
    [SerializeField] private float _maxAimAngle = 75f;

    [Header("Fisika Pegas Tangan Kiri")]
    [SerializeField] private float _springStiffness = 140f;
    [SerializeField] private float _springDamping = 14f;

    private float _currentReach = 0.35f;
    private float _reachVelocity;
    private Vector3 _currentTargetPos;
    private Vector3 _handVelocity;
    private float _currentAimAngle;
    private float _angleVelocity;

    public WeaponLogic EquippedWeapon => _equippedWeapon;

    private void Start()
    {
        if (_aimTracker == null) _aimTracker = GetComponent<PlayerAimTracker2D>();
        if (_lHandTarget != null) _currentTargetPos = _lHandTarget.position;
    }

    private void LateUpdate()
    {
        if (_aimTracker == null || _leftShoulderPivot == null || _lHandTarget == null) return;
        float facingDir = _gfxTransform != null ? Mathf.Sign(_gfxTransform.localScale.x) : 1f;

        ProcessLeftTriggerInput();
        if (_aimTracker.IsAimingLeft)
        {
            Vector2 toMouse = _aimTracker.MouseWorldPosition - _leftShoulderPivot.position;
            float distanceToMouse = toMouse.magnitude;

            _currentReach = ProceduralArmMath.UpdateSpringReach(
                distanceToMouse, 0.8f, 2.2f, _minReach, _maxReach,
                _springStiffness, _springDamping, ref _reachVelocity, _currentReach, Time.deltaTime
            );

            _currentAimAngle = ProceduralArmMath.UpdateAimAngle(
                toMouse, facingDir, _minAimAngle, _maxAimAngle, 0.08f, ref _angleVelocity, _currentAimAngle
            );

            float recoilOffset = (_equippedWeapon is GunLogic gun) ? gun.CurrentAimRecoil : 0f;
            float rad = (_currentAimAngle + recoilOffset) * Mathf.Deg2Rad;

            Vector3 targetOffset = new Vector3(Mathf.Cos(rad) * _currentReach * facingDir, Mathf.Sin(rad) * _currentReach, 0f);
            Vector3 desiredHandPos = _leftShoulderPivot.position + targetOffset;

            _currentTargetPos = Vector3.SmoothDamp(_currentTargetPos, desiredHandPos, ref _handVelocity, 0.04f);
            _lHandTarget.position = _currentTargetPos;

            float worldAimAngle = _currentAimAngle * facingDir;
            Quaternion handRotation = Quaternion.Euler(0f, 0f, worldAimAngle + (facingDir * _handAngleOffset));
            if (_lHandBone != null) _lHandBone.rotation = handRotation;
        }
        else 
        {
            Vector3 restPos = _leftShoulderPivot.position + new Vector3(_idleOffset.x * facingDir, _idleOffset.y, 0f);
            _currentTargetPos = Vector3.Lerp(_currentTargetPos, restPos, Time.deltaTime * 10f);
            _lHandTarget.position = _currentTargetPos;

            Quaternion restRotation = Quaternion.Euler(0f, 0f, -90f * facingDir);
            if (_lHandBone != null)
                _lHandBone.rotation = Quaternion.Slerp(_lHandBone.rotation, restRotation, Time.deltaTime * 10f);
                
            _currentReach = _minReach;
        }
    }

    private void ProcessLeftTriggerInput()
    {
        if (_equippedWeapon == null) return;

        BaseWeaponSO weaponData = _equippedWeapon.WeaponData;
        WeaponTriggerMode triggerMode = (weaponData != null) ? weaponData.TriggerMode : WeaponTriggerMode.HOLDANDRELEASE;

        if (triggerMode == WeaponTriggerMode.PRESSONLY)
        {
            if (Input.GetMouseButtonDown(1))
            {
                _equippedWeapon.TriggerAttack(gameObject);
            }
        }
        else if (triggerMode == WeaponTriggerMode.HOLDANDRELEASE)
        {
            if (Input.GetMouseButtonDown(1))
            {
                _equippedWeapon.OnTriggerDown(gameObject);
            }
            else if (Input.GetMouseButton(1))
            {
                _equippedWeapon.OnTriggerHeld(gameObject);
            }

            if (Input.GetMouseButtonUp(1))
            {
                _equippedWeapon.OnTriggerUp(gameObject);
            }
        }
    }

    #region Weapon Binding API

    public void SetEquippedWeapon(WeaponLogic weapon)
    {
        _equippedWeapon = weapon;
    }

    public void ClearEquippedWeapon()
    {
        _equippedWeapon = null;
    }

    #endregion
}