using UnityEngine;
using System;

[DisallowMultipleComponent]
public class GunEnergyCore : MonoBehaviour
{
    public event Action<GunSystemState> OnStateChanged;

    [Header("Status Runtime (Monitoring)")]
    [SerializeField] private GunSystemState _currentState = GunSystemState.IDLE;
    [SerializeField] private float _currentBattery;
    [SerializeField] private float _currentHeat;

    public GunSystemState CurrentState => _currentState;
    public float CurrentBattery => _currentBattery;
    public float CurrentHeat => _currentHeat;
    public float MaxBattery { get; private set; } = 100f;
    public float MaxHeat { get; private set; } = 100f;

    private float _batteryCostPerShot = 10f;
    private float _batteryRegenRate = 25f;
    private float _heatPerShot = 15f;
    private float _heatDissipationRate = 20f;
    private float _idleDelay = 1.75f;
    private float _overheatCooldown = 2.5f;
    private float _overheatRecoveryThreshold = 40f;

    private float _idleTimer;
    private float _overheatTimer;
    private bool _isRightTriggerHeld;
    private bool _isLeftTriggerHeld;
    private bool _isInitialized = false;

    public void InitializeCore(GunWeaponSO config)
    {
        if (config == null) return;

        MaxBattery = config.MaxBattery;
        _batteryCostPerShot = config.BatteryCostPerShot;
        _batteryRegenRate = config.BatteryRegenRate;

        MaxHeat = config.MaxHeat;
        _heatPerShot = config.HeatPerShot;
        _heatDissipationRate = config.HeatDissipationRate;
        _idleDelay = config.IdleDelay;
        _overheatCooldown = config.OverheatCooldown;
        _overheatRecoveryThreshold = config.OverheatRecoveryThreshold;

        _currentBattery = MaxBattery;
        _currentHeat = 0f;
        
        SetState(GunSystemState.IDLE);

        _isRightTriggerHeld = false;
        _isLeftTriggerHeld = false;
        _idleTimer = _idleDelay;
        _overheatTimer = 0f;
        _isInitialized = true;
    }

    public void ResetSystem()
    {
        _currentBattery = MaxBattery;
        _currentHeat = 0f;
        SetState(GunSystemState.IDLE);
        _isRightTriggerHeld = false;
        _isLeftTriggerHeld = false;
        _overheatTimer = 0f;
        _idleTimer = _idleDelay;
        _isInitialized = false;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        UpdateTimersAndStates();
        ProcessResourceRegeneration();
    }

    private void UpdateTimersAndStates()
    {
        if (_currentState == GunSystemState.OVERHEAT)
        {
            if (_overheatTimer > 0f)
                _overheatTimer -= Time.deltaTime;

            if (_overheatTimer <= 0f && _currentHeat <= _overheatRecoveryThreshold)
            {
                bool anyTriggerHeld = _isRightTriggerHeld || _isLeftTriggerHeld;
                SetState(anyTriggerHeld ? GunSystemState.FIGHT : GunSystemState.IDLE);
                _idleTimer = anyTriggerHeld ? 0f : _idleDelay;
            }
            return;
        }

        bool isAnyTriggerHeld = _isRightTriggerHeld || _isLeftTriggerHeld;

        if (isAnyTriggerHeld)
        {
            SetState(GunSystemState.FIGHT);
            _idleTimer = 0f;
        }
        else
        {
            if (_idleTimer < _idleDelay)
            {
                _idleTimer += Time.deltaTime;
                SetState(GunSystemState.FIGHT);
            }
            else
            {
                SetState(GunSystemState.IDLE);
            }
        }
    }

    private void ProcessResourceRegeneration()
    {
        bool anyTriggerHeld = _isRightTriggerHeld || _isLeftTriggerHeld;
        bool canDissipateHeat = (_currentState == GunSystemState.OVERHEAT) || (!anyTriggerHeld);

        if (canDissipateHeat && _currentHeat > 0f)
        {
            _currentHeat = Mathf.MoveTowards(_currentHeat, 0f, _heatDissipationRate * Time.deltaTime);
        }

        if (_currentState == GunSystemState.IDLE && _currentBattery < MaxBattery)
        {
            _currentBattery = Mathf.MoveTowards(_currentBattery, MaxBattery, _batteryRegenRate * Time.deltaTime);
        }
    }

    private void SetState(GunSystemState newState)
    {
        if (_currentState == newState) return;
        _currentState = newState;
        OnStateChanged?.Invoke(_currentState);
    }

    public void NotifyTriggerState(bool isRightHand, bool isHeld)
    {
        if (isRightHand) _isRightTriggerHeld = isHeld;
        else _isLeftTriggerHeld = isHeld;

        if (isHeld)
        {
            _idleTimer = 0f;
        }
    }

    public bool TryConsumeShot()
    {
        if (!_isInitialized) return false;

        if (_currentState == GunSystemState.OVERHEAT)
            return false;

        if (_currentBattery < _batteryCostPerShot)
            return false;

        _currentBattery = Mathf.Max(0f, _currentBattery - _batteryCostPerShot);
        _currentHeat = Mathf.Min(MaxHeat, _currentHeat + _heatPerShot);

        if (_currentHeat >= MaxHeat)
        {
            SetState(GunSystemState.OVERHEAT);
            _overheatTimer = _overheatCooldown;
        }

        _idleTimer = 0f;
        return true;
    }
}