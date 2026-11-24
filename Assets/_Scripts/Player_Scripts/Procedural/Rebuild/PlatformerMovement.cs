using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlatformerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player_Input _playerInput;
    [SerializeField] private Rigidbody2D _rigidbody;

    [Header("Pergerakan Horizontal")]
    [SerializeField] private float _speed = 6f;
    [SerializeField] private float _acceleration = 40f;
    [SerializeField] private float _deceleration = 30f;

    [Header("Juicy Effect: Micro-Recoil")]
    [Tooltip("Kecepatan meluruh recoil (semakin tinggi, recoil semakin cepat berhenti)")]
    [SerializeField] private float _recoilDecayRate = 25f;
    private float _recoilVelocityX;

    [Header("Lompatan & Gravitasi")]
    [SerializeField] private float _jumpForce = 12f;
    [SerializeField] private float _fallMultiplier = 3f;
    [SerializeField] private float _lowJumpMultiplier = 2f;

    [Header("Deteksi Lantai (Ground Check)")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private float _groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask _groundLayer;
    [Tooltip("Grace time sebelum dianggap lepas tanah (anti-flicker di tepi anak tangga)")]
    [SerializeField] private float _groundGraceTime = 0.08f;
    [Tooltip("Kecepatan jatuh yang langsung membatalkan grace time")]
    [SerializeField] private float _groundGraceFallSpeed = 2.5f;

    [Header("Sistem Panjat Tangga 2D")]
    [SerializeField] private float _stepHeight = 0.5f;
    [SerializeField] private float _stepSearchDist = 0.45f;
    [SerializeField] private float _maxSlopeAngle = 45f;
    [Tooltip("Sudut minimum normal permukaan untuk dianggap RISER tangga (normal riser ~90, lereng <45)")]
    [SerializeField] private float _stairFaceMinAngle = 80f;
    [Tooltip("Kecepatan naik halus saat menapaki anak tangga (m/s)")]
    [SerializeField] private float _stepClimbSpeed = 4.5f;
    [Tooltip("Jarak probe ke depan dari riser untuk mencari tinggi tread")]
    [SerializeField] private float _treadProbeForward = 0.12f;

    [Header("Sinkronisasi Animasi Prosedural")]
    [Tooltip("Ambang kecepatan untuk menganggap karakter masih bergerak walau input lepas")]
    [SerializeField] private float _stopVelocityThreshold = 0.15f;
    [Tooltip("Akselerasi saat berbalik arah (velocity & input berlawanan)")]
    [SerializeField] private float _reversalAcceleration = 15f;

    public Rigidbody2D PlayerRigidbody { get => _rigidbody; set => _rigidbody = value; }
    public float Speed { get => _speed; set => _speed = value; }
    public bool Freeze { get; set; }
    public bool IsGrounded => _isGrounded;
    public bool IsMoving => Mathf.Abs(_playerInput.InputX) > 0.1f;
    public bool IsMovingOrSliding => IsMoving || Mathf.Abs(_rigidbody.linearVelocity.x) > _stopVelocityThreshold;

    /// <summary>Arah input horizontal: -1, 0, atau 1. Dipakai agar langkah mundur tidak memakai sign velocity yang masih tertinggal.</summary>
    public float MoveInputDirection
    {
        get
        {
            if (_playerInput == null) return 0f;
            float x = _playerInput.InputX;
            return Mathf.Abs(x) > 0.1f ? Mathf.Sign(x) : 0f;
        }
    }

    private bool _isGrounded;
    private bool _isClimbing;
    private float _groundGraceTimer;
    private float _climbRemaining;

    private void Start()
    {
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody2D>();
        _rigidbody.freezeRotation = true;
    }

    private void Update()
    {
        UpdateGroundedState();

        if (Freeze) return;
        if (Input.GetButtonDown("Jump") && _isGrounded)
            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, _jumpForce);
    }

    private void FixedUpdate()
    {
        if (Freeze) return;

        float dt = Time.fixedDeltaTime;
        _recoilVelocityX = Mathf.MoveTowards(_recoilVelocityX, 0f, _recoilDecayRate * dt);

        float speedMultiplier = _isClimbing ? 0.6f : 1.0f;
        float targetSpeed = _playerInput.InputX * (_speed * speedMultiplier);
        float speedDiff = targetSpeed - _rigidbody.linearVelocity.x;

        bool reversing = Mathf.Abs(targetSpeed) > 0.01f
            && Mathf.Abs(_rigidbody.linearVelocity.x) > 0.01f
            && Mathf.Sign(targetSpeed) != Mathf.Sign(_rigidbody.linearVelocity.x);

        float accelRate;
        if (Mathf.Abs(targetSpeed) <= 0.01f) accelRate = _deceleration;
        else if (reversing) accelRate = _reversalAcceleration;
        else accelRate = _acceleration;

        float movement = speedDiff * accelRate * dt;
        float finalVelX = _rigidbody.linearVelocity.x + movement + _recoilVelocityX;
        _rigidbody.linearVelocity = new Vector2(finalVelX, _rigidbody.linearVelocity.y);

        if (IsMoving && _isGrounded)
            HandleStepClimb();
        else
            CancelStepClimb();

        if (_rigidbody.linearVelocity.y < 0)
        {
            _rigidbody.linearVelocity += Vector2.up * (Physics2D.gravity.y * (_fallMultiplier - 1) * dt);
        }
        else if (_rigidbody.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            _rigidbody.linearVelocity += Vector2.up * (Physics2D.gravity.y * (_lowJumpMultiplier - 1) * dt);
        }

        ApplyStepClimb();
    }

    public void ApplyMicroRecoil(float recoilForce, float hitDirectionX) => _recoilVelocityX = -hitDirectionX * recoilForce;

    private void UpdateGroundedState()
    {
        bool detected = _groundCheckPoint != null &&
                        Physics2D.OverlapCircle(_groundCheckPoint.position, _groundCheckRadius, _groundLayer);

        if (detected || _isClimbing)
        {
            _isGrounded = true;
            _groundGraceTimer = 0f;
            return;
        }

        // Jatuh nyata membatalkan grace time agar lompat tidak terasa "lengket".
        if (_rigidbody != null && _rigidbody.linearVelocity.y < -_groundGraceFallSpeed)
        {
            _isGrounded = false;
            _groundGraceTimer = 0f;
            return;
        }

        _groundGraceTimer += Time.deltaTime;
        if (_groundGraceTimer >= _groundGraceTime)
            _isGrounded = false;
    }

    private void HandleStepClimb()
    {
        // Climb yang sedang berjalan diselesaikan oleh ApplyStepClimb.
        if (_isClimbing) return;

        if (_groundCheckPoint == null) return;

        float direction = Mathf.Sign(_playerInput.InputX);
        if (Mathf.Abs(direction) < 0.01f)
        {
            CancelStepClimb();
            return;
        }

        Vector2 originLower = new Vector2(transform.position.x + (direction * 0.15f), _groundCheckPoint.position.y + 0.08f);
        Vector2 originUpper = new Vector2(transform.position.x + (direction * 0.15f), _groundCheckPoint.position.y + _stepHeight);
        RaycastHit2D hitLower = Physics2D.Raycast(originLower, Vector2.right * direction, _stepSearchDist, _groundLayer);
        RaycastHit2D hitUpper = Physics2D.Raycast(originUpper, Vector2.right * direction, _stepSearchDist, _groundLayer);

        if (hitLower.collider == null || hitUpper.collider != null)
        {
            CancelStepClimb();
            return;
        }

        float faceAngle = Vector2.Angle(hitLower.normal, Vector2.up);
        if (faceAngle < _stairFaceMinAngle)
        {
            CancelStepClimb();
            return;
        }

        // Cari tinggi tread di balik riser agar badan naik tepat setinggi anak tangga.
        float probeX = hitLower.point.x + direction * _treadProbeForward;
        Vector2 treadOrigin = new Vector2(probeX, hitLower.point.y + _stepHeight + 0.1f);
        RaycastHit2D treadHit = Physics2D.Raycast(treadOrigin, Vector2.down, _stepHeight + 0.4f, _groundLayer);

        float treadY = treadHit.collider != null && treadHit.normal.y > 0.5f
            ? treadHit.point.y
            : _groundCheckPoint.position.y + 0.1f;

        float rise = treadY - _groundCheckPoint.position.y;
        _climbRemaining = Mathf.Max(0f, rise + 0.02f);
        _isClimbing = _climbRemaining > 0.01f;
    }

    private void ApplyStepClimb()
    {
        if (!_isClimbing) return;

        float climbStep = _stepClimbSpeed * Time.fixedDeltaTime;
        _climbRemaining -= climbStep;

        float climbVel = Mathf.Max(_rigidbody.linearVelocity.y, _stepClimbSpeed);
        _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, climbVel);

        if (_climbRemaining <= 0f)
        {
            _isClimbing = false;
            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 0f);
        }
    }

    private void CancelStepClimb()
    {
        _isClimbing = false;
        _climbRemaining = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint != null)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_groundCheckPoint.position, _groundCheckRadius);
            Gizmos.color = Color.cyan;
            float dir = transform.localScale.x >= 0 ? 1f : -1f;
            Vector3 originLower = new Vector3(transform.position.x + (dir * 0.1f), _groundCheckPoint.position.y + 0.08f, 0f);
            Vector3 originUpper = new Vector3(transform.position.x + (dir * 0.1f), _groundCheckPoint.position.y + _stepHeight, 0f);
            Gizmos.DrawLine(originLower, originLower + Vector3.right * dir * _stepSearchDist);
            Gizmos.DrawLine(originUpper, originUpper + Vector3.right * dir * _stepSearchDist);
        }
    }
}
