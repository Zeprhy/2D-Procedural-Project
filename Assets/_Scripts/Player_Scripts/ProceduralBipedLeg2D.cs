using UnityEngine;
using System.Collections;
using System;

public class ProceduralBipedLeg2D : MonoBehaviour
{
    [Header("Referensi")]
    public Transform footHome;
    public ProceduralBipedLeg2D otherLeg;
    public LayerMask groundLayer;
    public PlatformerMovement movement;
    [SerializeField] private Transform gfxTransform;


    [Header("Pengaturan Langkah")]
    public float stepDistance = 0.35f;   // Jarak minimal sebelum kaki melangkah
    public float stepHeight = 0.25f;     // Tinggi angkatan kaki saat melangkah
    public float stepSpeed = 12f;       // Kecepatan ayunan kaki
    public float stepOvershoot = 0.45f;  // Seberapa jauh kaki mendahului ke depan saat melangkah
    public float footYOffset = 0.05f;

    public bool isMyTurn = true;
    public bool IsStepping { get; private set; } = false;

    private Quaternion _targetRotation = Quaternion.identity;
    void Update()
    {
        if (footHome == null || movement == null || gfxTransform == null) return;

        if (!movement.IsGrounded)
        {
            StopAllCoroutines();
            IsStepping = false;
            transform.position = Vector3.Lerp(transform.position, footHome.position, Time.deltaTime * 12f);
            transform.rotation = Quaternion.Slerp(transform.rotation, footHome.rotation, Time.deltaTime * 12f);
            return;
        }

        if (!movement.IsMoving && !IsStepping)
        {   
            SnapFootToGroundIdle();
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * 15f);
        if (IsStepping || !isMyTurn || (otherLeg != null && otherLeg.IsStepping)) return;

        float modeDir = Input.GetAxisRaw("Horizontal");
        float distToHome = Vector2.Distance(transform.position, footHome.position);
        if (distToHome > stepDistance)
        {
            StartCoroutine(PerformStep(Mathf.Sign(modeDir)));
        }        
    }

    private void SnapFootToGroundIdle()
    {
        RaycastHit2D hit = Physics2D.Raycast(footHome.position + Vector3.up * 0.5f, Vector2.down, 2f, groundLayer);
        if (hit.collider != null)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            if (slopeAngle <= 45f)
            {
                Vector3 target = new Vector3(hit.point.x, hit.point.y + footYOffset, transform.position.z);
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 15f);
                AlignWithNormal(hit.normal);
            }
        }
    }

    IEnumerator PerformStep(float moveDir)
    {
        IsStepping = true;
        Vector3 startPos = transform.position; 

        float currentSpeed = Mathf.Abs(movement.PlayerRigidbody.linearVelocity.x);
        float dynamicDistance = stepDistance + (currentSpeed * 0.05f);

        float stanceX = otherLeg != null ? otherLeg.transform.position.x : footHome.position.x;
        float targetX = stanceX + (moveDir * dynamicDistance);

        // Tembak laser dari atas ke bawah untuk mencari permukaan anak tangga / lereng
        Vector2 rayOrigin = new Vector2(targetX, footHome.position.y + 1.5f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 4f, groundLayer);

        Vector3 targetPos = new Vector3(targetX, footHome.position.y, transform.position.z);
        if (hit.collider != null)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            if (slopeAngle <= 45f)
            {
                targetPos = new Vector3(hit.point.x, hit.point.y + footYOffset, transform.position.z);
                AlignWithNormal(hit.normal);
            }
        }

        float t = 0f;
        while (t < 1f)
        {
            if (!movement.IsGrounded) break;

            t += Time.deltaTime * stepSpeed;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, smoothT);
            currentPos.y += Mathf.Sin(smoothT * Mathf.PI) * stepHeight;

            transform.position = currentPos;
            yield return null;
        }

        transform.position = targetPos;
        IsStepping = false;

        if (otherLeg != null)
        {
            this.isMyTurn = false;
            otherLeg.isMyTurn = true;
        }
    }

    private void AlignWithNormal(Vector2 normal)
    {
        if (normal.y <= 0.4f) return;

        float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
        angle = Mathf.Clamp(angle, -45f, 45f);
        _targetRotation = Quaternion.Euler(0, 0, angle);
    }

    void OnDrawGizmos()
    {
        if (footHome != null)
        {
            Gizmos.color = IsStepping ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, footHome.position);
        }
    }
}