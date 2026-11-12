using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlatformerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player_Input _playerInput;
    [SerializeField] private Rigidbody2D _rigidbody;

    [Header("Pergerakan Horizontal")]
    [SerializeField] private float _speed = 7f;
    [SerializeField] private float _acceleration = 40f;
    [SerializeField] private float _deceleration = 30f;

    [Header("Lompatan & Gravitasi (Responsive Jump)")]
    [SerializeField] private float _jumpForce = 14f;
    [SerializeField] private float _fallMultiplier = 3f;      // Jatuh lebih mantap
    [SerializeField] private float _lowJumpMultiplier = 2f;   // Lepas spasi lebih awal = lompat pendek

    [Header("Deteksi Lantai (Ground Check)")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private float _groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask _groundLayer;

    // Properti publik (dibaca oleh Player_Animator & Foot IK)
    public Rigidbody2D PlayerRigidbody { get => _rigidbody; set => _rigidbody = value; }
    public float Speed { get => _speed; set => _speed = value; }
    public bool Freeze { get; set; }
    public bool IsGrounded => _isGrounded;
    public bool IsMoving => Mathf.Abs(_playerInput.InputX) > 0.1f;

    private bool _isGrounded;

    private void Start()
    {
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody2D>();
        _rigidbody.freezeRotation = true;
    }

    private void Update()
    {
        if (_groundCheckPoint != null)
            _isGrounded = Physics2D.OverlapCircle(_groundCheckPoint.position, _groundCheckRadius, _groundLayer);

        if (Freeze) return;

        if (Input.GetButtonDown("Jump") && _isGrounded)
            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, _jumpForce);
    }

    private void FixedUpdate()
    {
        if (Freeze) return;

        // Gerak Horizontal di Sumbu X (Platformer murni)
        float targetSpeed = _playerInput.InputX * _speed;
        float speedDiff = targetSpeed - _rigidbody.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? _acceleration : _deceleration;
        float movement = speedDiff * accelRate * Time.fixedDeltaTime;

        _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x + movement, _rigidbody.linearVelocity.y);

        // Fisika Gravitasi Lompat Responsif
        if (_rigidbody.linearVelocity.y < 0)
        {
            _rigidbody.linearVelocity += Vector2.up * (Physics2D.gravity.y * (_fallMultiplier - 1) * Time.fixedDeltaTime);
        }
        else if (_rigidbody.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            _rigidbody.linearVelocity += Vector2.up * (Physics2D.gravity.y * (_lowJumpMultiplier - 1) * Time.fixedDeltaTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint != null)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_groundCheckPoint.position, _groundCheckRadius);
        }
    }
}