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
    public float stepDistance = 0.5f;
    public float stepHeight = 0.2f;
    public float stepSpeed = 10f;
    public float footYOffset = 0.05f;
    public float maxSlopeAngle = 45f;
    public bool isMyTurn = true;
    
    [Header("Pose Istirahat (Idle Stance)")]
    [Tooltip("Jarak lateral tambahan dari footHome saat idle. Arah otomatis menjauh dari kaki lain (world-space, aman saat flip).")]
    public float idleStanceWidth = 0.10f;

    public bool IsStepping { get; private set; } = false;
    private Quaternion _targetRotation = Quaternion.identity;

    // (F) smoothing frame-rate independent; rate 15 dipertahankan agar feel lama sama
    private const float SmoothRate = 15f;
    private static float ExpT(float dt) => 1f - Mathf.Exp(-SmoothRate * dt);

    private void Update()
    {
        if (footHome == null || movement == null) return;

        // Kondisi di udara (Lompat / Jatuh) — perilaku lama dipertahankan
        if (!movement.IsGrounded)
        {
            StopAllCoroutines();
            IsStepping = false;
            float t = ExpT(Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, footHome.position, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, footHome.rotation, t);
            return;
        }

        // (E) gate idle memakai gerakan AKTUAL (input OR momentum), bukan input saja
        bool actuallyMoving = movement.IsMovingOrSliding;

        // Kondisi diam (Idle)
        if (!actuallyMoving && !IsStepping)
        {
            SnapFootToGroundIdle();
            // (A) FIX: rotasi telapak tetap diperbarui saat idle (sebelumnya return sebelum slerp)
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, ExpT(Time.deltaTime));
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, ExpT(Time.deltaTime));
        if (IsStepping || !isMyTurn || (otherLeg != null && otherLeg.IsStepping)) return;

        // (E) arah langkah: fallback ke sign velocity saat input lepas tapi masih momentum
        float moveDir = Mathf.Sign(Input.GetAxisRaw("Horizontal"));
        if (moveDir == 0f) moveDir = Mathf.Sign(movement.PlayerRigidbody.linearVelocity.x);
        if (moveDir == 0f) return;

        float distToHome = Vector2.Distance(transform.position, footHome.position);
        if (distToHome > stepDistance)
        {
            StartCoroutine(PerformStep(moveDir));
        }
    }

    private void SnapFootToGroundIdle()
    {
        // Arah stance otomatis = menjauh dari kaki lain (world-space, aman saat flip kiri/kanan)
        float stanceSign = (otherLeg != null && otherLeg.footHome != null)
            ? Mathf.Sign(footHome.position.x - otherLeg.footHome.position.x)
            : 0f;
        if (stanceSign == 0f)
            stanceSign = gameObject.name.StartsWith("L") ? -1f : 1f;

        Vector3 probe = footHome.position + new Vector3(idleStanceWidth * stanceSign, 0.5f, 0f);
        RaycastHit2D hit = Physics2D.Raycast(probe, Vector2.down, 2.5f, groundLayer);
        if (hit.collider != null)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            if (slopeAngle <= maxSlopeAngle)
            {
                Vector3 target = new Vector3(hit.point.x, hit.point.y + footYOffset, transform.position.z);
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 15f);
                AlignWithNormal(hit.normal);
            }
        }
    }

    private IEnumerator PerformStep(float moveDir)
    {
        IsStepping = true;
        Vector3 startPos = transform.position;
        float currentSpeed = Mathf.Abs(movement.PlayerRigidbody.linearVelocity.x);
        float dynamicDistance = stepDistance + (currentSpeed * 0.03f);

        float stanceX = otherLeg != null ? otherLeg.transform.position.x : footHome.position.x;
        float targetX = stanceX + (moveDir * dynamicDistance);
        Vector2 rayOrigin = new Vector2(targetX, footHome.position.y + 1.5f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 4f, groundLayer);

        // (D) FIX: validasi permukaan SEBELUM ayunan dimulai. Tidak ada lagi target melayang.
        if (hit.collider == null) { AbortStep(); yield break; }
        float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
        if (slopeAngle > maxSlopeAngle) { AbortStep(); yield break; }

        Vector3 targetPos = new Vector3(hit.point.x, hit.point.y + footYOffset, transform.position.z);
        AlignWithNormal(hit.normal);

        float t = 0f;
        while (t < 1f)
        {
            if (!movement.IsGrounded) break;
            t += Time.deltaTime * stepSpeed;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, smoothT);
            currentPos.y += Mathf.Sin(t * MathF.PI) * stepHeight;
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

    // (D) pembatalan step aman: kaki tetap menapak di tempatnya, giliran dilepas ke kaki lain
    private void AbortStep()
    {
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
        angle = Mathf.Clamp(angle, -maxSlopeAngle, maxSlopeAngle);
        _targetRotation = Quaternion.Euler(0, 0, angle);
    }

    private void OnDrawGizmos()
    {
        if (footHome != null)
        {
            Gizmos.color = IsStepping ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, footHome.position);
        }
    }
}