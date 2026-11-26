using UnityEngine;

/// <summary>
/// Mengatur state animasi dummy (Idle / Hit / Death) melalui Animator.
/// Dipanggil oleh DummyEntity saat menerima damage atau mati.
/// </summary>
public class DummyAnimationController : MonoBehaviour
{
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    private const string IdleStateName = "Idle";

    [SerializeField] private Animator _animator;

    public bool IsDead { get; private set; }

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        PlayIdle();
    }

    /// <summary>Kembali ke state Idle (loop).</summary>
    public void PlayIdle()
    {
        if (IsDead || _animator == null) return;

        _animator.ResetTrigger(HitHash);
        _animator.ResetTrigger(DeadHash);
        _animator.Play(IdleStateName, 0, 0f);
    }

    /// <summary>Mainkan animasi Hit sekali, lalu otomatis kembali ke Idle.</summary>
    public void PlayHit()
    {
        if (IsDead || _animator == null) return;

        _animator.ResetTrigger(DeadHash);
        _animator.SetTrigger(HitHash);
    }

    /// <summary>Mainkan animasi Death sekali dan tahan di frame terakhir.</summary>
    public void PlayDeath()
    {
        if (IsDead) return;
        IsDead = true;

        if (_animator == null) return;

        _animator.ResetTrigger(HitHash);
        _animator.SetTrigger(DeadHash);
    }
}
