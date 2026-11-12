// using UnityEngine;

// [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
// public class PlayerController2D : MonoBehaviour
// {
//     [Header("Referensi Visual & Animasi")]
//     public Transform gfxTransform; // Objek GFX (yang akan di-flip kiri/kanan)
//     public Animator animator;

//     [Header("Pergerakan Horizontal")]
//     public float moveSpeed = 6f;
//     public float acceleration = 40f;
//     public float deceleration = 30f;

//     [Header("Lompatan & Gravitasi (Responsive Jump)")]
//     public float jumpForce = 12f;
//     public float fallMultiplier = 2.5f;   
//     public float lowJumpMultiplier = 2f; 

//     [Header("Deteksi Lantai (Ground Check)")]
//     public Transform groundCheckPoint;
//     public float groundCheckRadius = 0.2f;
//     public LayerMask groundLayer;

//     // Komponen Internal
//     private Rigidbody2D rb;
//     private float horizontalInput;
//     private bool isGrounded;
//     private bool isFacingRight = true;

//     // Properti publik untuk dibaca script Foot IK
//     public bool IsMoving => Mathf.Abs(horizontalInput) > 0.1f;
//     public bool IsGrounded => isGrounded;


//     void Start()
//     {
//         rb = GetComponent<Rigidbody2D>();

//         rb.freezeRotation = true;

//         if (animator == null && gfxTransform != null)
//             animator = gfxTransform.GetComponent<Animator>();
//     }

//     void Update()
//     {
//         horizontalInput = Input.GetAxis("Horizontal");

//         if (groundCheckPoint != null)
//             isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);

//         if (Input.GetButtonDown("Jump") && isGrounded)
//             rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        
//         if (animator != null)
//         {
//             animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
//             animator.SetBool("IsGrounded", isGrounded);
//         }
//     }

//     void FixedUpdate()
//     {
//         float targetSpeed = horizontalInput * moveSpeed;
//         float speedDif = targetSpeed - rb.linearVelocity.x;
//         float accelRate = (Mathf.Abs(targetSpeed) > 0.01f)? acceleration : deceleration;
//         float movement = speedDif * accelRate * Time.fixedDeltaTime;

//         rb.linearVelocity = new Vector2(rb.linearVelocity.x + movement, rb.linearVelocity.y);

//         if (rb.linearVelocity.y < 0)
//             rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime);
//         else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
//             rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime);
//     }

//     void OnDrawGizmosSelected()
//     {
//         if (groundCheckPoint != null)
//         {
//             Gizmos.color = isGrounded ? Color.green : Color.red;
//             Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
//         }
//     }
// }