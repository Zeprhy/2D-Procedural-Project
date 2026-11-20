using UnityEngine;


[DisallowMultipleComponent]
public class GunEnergyCore : MonoBehaviour
{
    // State & Sumber Daya Runtime
    public GunSystemState CurrentState { get; private set; } = GunSystemState.IDLE;
    public float CurrentBattery { get; private set; }
    public float CurrentHeat { get; private set; }
    public float MaxBattery { get; private set; } = 100f;
    public float MaxHeat { get; private set; } = 100f;

    // Parameter Internal dari GunWeaponSO
    private float _batteryCostPerShot = 10f;
    private float _batteryRegenRate = 25f;
    private float _heatPerShot = 15f;
    private float _heatDissipationRate = 20f;
    private float _idleDelay = 1.75f;
    private float _overheatCooldown = 2.5f;
    private float _overheatRecoveryThreshold = 40f;

    // Timers & Triggers Tracking
    private float _idleTimer;
    private float _overheatTimer;
    private bool _isRightTriggerHeld;
    private bool _isLeftTriggerHeld;

    private bool _isInitialized = false;

    /// <summary>
    /// Diinisialisasi oleh WeaponEquipmentManager saat Dual Pistol dipasang
    /// </summary>
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

        CurrentBattery = MaxBattery;
        CurrentHeat = 0f;
        CurrentState = GunSystemState.IDLE;

        _isRightTriggerHeld = false;
        _isLeftTriggerHeld = false;
        _idleTimer = _idleDelay;
        _overheatTimer = 0f;
        _isInitialized = true;
    }

    /// <summary>
    /// Direset saat Dual Pistol dilepas/di-drop
    /// </summary>
    public void ResetSystem()
    {
        CurrentBattery = MaxBattery;
        CurrentHeat = 0f;
        CurrentState = GunSystemState.IDLE;
        _isRightTriggerHeld = false;
        _isLeftTriggerHeld = false;
        _isInitialized = false;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        
    }

    private void UpdateTimerAndStates()
    {
        if (CurrentState == GunSystemState.OVERHEAT)
        {
            if (_overheatTimer > 0)
                _overheatTimer -= Time.deltaTime;

            if (_overheatTimer <= 0f && CurrentHeat <= _overheatRecoveryThreshold)
            {
                CurrentState = (_isRightTriggerHeld || _isLeftTriggerHeld) ? GunSystemState.FIGHT : GunSystemState.IDLE;
                _idleTimer = 0f;
            }

            return;
        }

        bool anyTriggerHeld = _isRightTriggerHeld || _isLeftTriggerHeld;

        if (anyTriggerHeld)
        {
            CurrentState = GunSystemState.FIGHT;
            _idleTimer = 0f;
        }
        else
        {
            if (_idleTimer < _idleDelay)
            {
                _idleTimer += Time.deltaTime;
                CurrentState = GunSystemState.FIGHT;
            }
            else
                CurrentState = GunSystemState.IDLE;
        }     
    }

    private void ProcessResourceRegeneration()
    {
        if (CurrentHeat > 0f)
            CurrentHeat = Mathf.MoveTowards(CurrentHeat, 0f, _heatDissipationRate * Time.deltaTime);
        
        if (CurrentState == GunSystemState.IDLE && CurrentBattery < MaxBattery)
            CurrentBattery = Mathf.MoveTowards(CurrentBattery, MaxBattery, _batteryRegenRate * Time.deltaTime);
    }

    #region Trigger Event Notifications (Single Authority from Controllers)

    public void NotifyTriggerState(bool isRightHand, bool isHeld)
    {
        if (isRightHand) _isRightTriggerHeld = isHeld;
        else _isLeftTriggerHeld = isHeld;

        if (isHeld)
        {
            _idleTimer = 0f;
        }
    }

    #endregion

    #region Atomic consumption API

    /// <summary>
    /// Memvalidasi dan mengeksekusi konsumsi energi tembakan secara atomik
    /// Mengembalikan true HANYA jika tembakan diizinkan dan resource berhasil dipotong
    /// </summary>
    
    public bool TryConsumeShoot()
    {
        if (!_isInitialized) return false;
        if (CurrentState == GunSystemState.OVERHEAT) return false;
        if (CurrentBattery < _batteryCostPerShot) return false;

        CurrentBattery = Mathf.Max(0f, CurrentBattery - _batteryCostPerShot);
        CurrentHeat = Mathf.Min(MaxHeat, CurrentHeat + _heatPerShot);

        if (CurrentHeat >= MaxHeat)
        {
            CurrentState = GunSystemState.OVERHEAT;
            _overheatTimer = _overheatCooldown;
        }


        _idleTimer = 0f;
        return true;
    }

    #endregion

}