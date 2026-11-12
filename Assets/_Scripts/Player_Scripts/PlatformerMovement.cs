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
    [SerializeField] private float _fallMultiplier = 3f;  
    [SerializeField] private float _lowJumpMultiplier = 2f; 

    [Header("Deteksi Lantai (Ground Check)")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private float _groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask _groundLayer;

    [Header ("System Panjat Tangga 2D Step Up")]
    [SerializeField] private float _stepHeight = 0.3f;       
    [SerializeField] private float _stepSearchDist = 0.4f; 
    [SerializeField] private float _stepSmooth = 0.15f;   
    [SerializeField] private float _maxSlopeAngle = 45f;


    // Properti publik (dibaca oleh Player_Animator & Foot IK)
    public Rigidbody2D PlayerRigidbody { get => _rigidbody; set => _rigidbody = value; }
    public float Speed { get => _speed; set => _speed = value; }
    public bool Freeze { get; set; }
    public bool IsGrounded => _isGrounded;
    public bool IsMoving => Mathf.Abs(_playerInput.InputX) > 0.1f;

    private bool _isGrounded;
    private bool _isClimbing;

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
        float speedMultiplier = _isClimbing ? 0.6f : 1.0f; 
        float targetSpeed = _playerInput.InputX * (_speed * speedMultiplier);

        float speedDiff = targetSpeed - _rigidbody.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? _acceleration : _deceleration;
        float movement = speedDiff * accelRate * Time.fixedDeltaTime;

        _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x + movement, _rigidbody.linearVelocity.y);

        if (IsMoving && _isGrounded)
            HandleStepClimb();

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

    private void HandleStepClimb()
    {
        float direction = Mathf.Sign(_playerInput.InputX);
        Vector2 originLower = new Vector2(transform.position.x + (direction * 0.1f), _groundCheckPoint.position.y + 0.08f);
        Vector2 originUpper = new Vector2(transform.position.x + (direction * 0.1f), _groundCheckPoint.position.y + _stepHeight);

        // Tembak laser bawah dan laser atas ke arah jalan
        RaycastHit2D hitLower = Physics2D.Raycast(originLower, Vector2.right * direction, _stepSearchDist, _groundLayer);
        RaycastHit2D hitUpper = Physics2D.Raycast(originUpper, Vector2.right * direction, _stepSearchDist, _groundLayer);

        if (hitLower.collider != null && hitUpper.collider == null)
        {   
            float surfaceAngle = Vector2.Angle(hitLower.normal, Vector2.up);

            if (surfaceAngle > _maxSlopeAngle && surfaceAngle < 85f)
            {
                _isClimbing = false;
                return;
            }
            _isClimbing = true;
            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 2.0f);
        }
        else
        {
            _isClimbing = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint != null)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_groundCheckPoint.position, _groundCheckRadius);

            Gizmos.color = Color.cyan;
            float dir = transform.localScale.x >= 0 ? 1f : -1f;
            Vector3 originLower = new Vector3(transform.position.x, _groundCheckPoint.position.y + 0.05f, 0f);
            Vector3 originUpper = new Vector3(transform.position.x, _groundCheckPoint.position.y + _stepHeight, 0f);
            
            Gizmos.DrawLine(originLower, originLower + Vector3.right * dir * _stepSearchDist);
            Gizmos.DrawLine(originUpper, originUpper + Vector3.right * dir * _stepSearchDist);
        }
    }
}