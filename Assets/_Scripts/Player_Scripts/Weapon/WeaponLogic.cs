using UnityEngine;
using System.Collections;

public abstract class WeaponLogic : MonoBehaviour
{
    [Header("Data Senjata (Base)")]
    [SerializeField] protected BaseWeaponSO _weaponData;

    [Header("Juicy Effect: Hit Stop / Freeze Frame (Common)")]
    [Tooltip("Nilai timeScale saat freeze (0.05f = micro slow-mo dramatis, 0.0f = pure freeze)")]
    [SerializeField] protected float _hitStopTimeScale = 0.05f;
    [SerializeField] protected float _normalHitStopDuration = 0.035f;
    [SerializeField] protected float _criticalHitStopDuration = 0.09f;

    [Header("Juicy Effect: Combat Feedback References")]
    [Tooltip("Komponen pengontrol efek visual benturan tebasan/tembakan")]
    [SerializeField] protected CombatVFXController _vfxController;
    [Tooltip("Prefab teks angka damage yang melayang")]
    [SerializeField] protected DamagePopUp2D _damageTextPrefab;

    public BaseWeaponSO WeaponData => _weaponData;
    public bool IsAttacking { get; protected set; }

    protected GameObject _currentOwner;
    private float _hitStopEndTime;
    private Coroutine _hitStopRoutine;

    protected virtual void Awake()
    {
        if (_vfxController == null)
            _vfxController = GetComponent<CombatVFXController>();

        if (_vfxController == null)
            _vfxController = GetComponentInParent<CombatVFXController>();

        if (_vfxController == null)
            _vfxController = FindFirstObjectByType<CombatVFXController>();
    }

    protected virtual void OnDisable()
    {
        RestoreTimeScale();
    }

    public void SetWeaponData(BaseWeaponSO data)
    {
        _weaponData = data;
    }

    public abstract void TriggerAttack(GameObject owner);

    #region Trigger Lifecycle API

    public virtual void OnTriggerDown(GameObject owner)
    {
        _currentOwner = owner;
    }

    public virtual void OnTriggerHeld(GameObject owner)
    {
        _currentOwner = owner;
    }

    public virtual void OnTriggerUp(GameObject owner)
    {
        _currentOwner = owner;
    }

    #endregion

    #region Hit Stop Management

    public void RequestHitStop(bool isCritical)
    {
        float duration = isCritical ? _criticalHitStopDuration : _normalHitStopDuration;
        if (duration <= 0f) return;

        float newEndTime = Time.unscaledTime + duration;
        if (newEndTime > _hitStopEndTime)
            _hitStopEndTime = newEndTime;

        if (_hitStopRoutine == null)
            _hitStopRoutine = StartCoroutine(HitStopWorker());
    }

    private IEnumerator HitStopWorker()
    {
        Time.timeScale = _hitStopTimeScale;

        while (Time.unscaledTime < _hitStopEndTime)
        {
            yield return null;
        }

        RestoreTimeScale();
    }

    protected void RestoreTimeScale()
    {
        Time.timeScale = 1.0f;
        _hitStopEndTime = 0f;
        if (_hitStopRoutine != null)
        {
            StopCoroutine(_hitStopRoutine);
            _hitStopRoutine = null;
        }
    }

    #endregion
}