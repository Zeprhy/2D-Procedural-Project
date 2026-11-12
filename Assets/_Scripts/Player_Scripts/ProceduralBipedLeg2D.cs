using UnityEngine;
using System.Collections;

public class ProceduralBipedLeg2D : MonoBehaviour
{
    [Header("Referensi")]
    public Transform footHome;
    public ProceduralBipedLeg2D otherLeg;
    public LayerMask groundLayer;
    public PlatformerMovement movement;

    [Header("Pengaturan Langkah")]
    public float stepDistance = 0.4f;   // Jarak sebelum melangkah
    public float stepHeight = 0.22f;     // Tinggi angkatan kaki saat melangkah
    public float stepSpeed = 10f;       // Ayunan kaki lebih lincah
    public float stepOvershoot = 0.35f;
    public float footYOffset = 0.05f;

    public bool IsStepping { get; private set; } = false;

    void Update()
    {
        if (footHome == null) return;
        if (IsStepping) return;

        if (otherLeg != null && otherLeg.IsStepping) return;

        float diffX = footHome.position.x - transform.position.x;
        float InputX = Input.GetAxisRaw("Horizontal");

        // Saat jalan maju (inputX > 0), HANYA kaki yang tertinggal di BELAKANG (diffX > 0) yang boleh mengayun!
        // Saat jalan mundur (inputX < 0), HANYA kaki yang tertinggal di DEPAN (diffX < 0) yang boleh mengayun!
        bool isTrailingLeg = (InputX > 0f && diffX > stepDistance) || (InputX < 0f && diffX < -stepDistance);

        if (isTrailingLeg && movement.IsGrounded)
        {
            float moveDir = Mathf.Sign(InputX);
            StartCoroutine(PerformStep(moveDir));
        }
    }

    IEnumerator PerformStep(float moveDir)
    {
        IsStepping = true;
        Vector3 startPos = transform.position;

        float targetX = footHome.position.x + (moveDir * stepOvershoot);
        Vector2 rayOrigin = new Vector2(targetX, footHome.position.y + 1f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 3f, groundLayer);

        Vector3 targetPos = new Vector3(targetX, footHome.position.y, transform.position.z);
        if (hit.collider != null)
        {
            targetPos = new Vector3(hit.point.x, hit.point.y + footYOffset, transform.position.z);
        }

        float progress = 0f;

        while (progress < 1f)
        {
            progress += Time.deltaTime * stepSpeed;
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);
            currentPos.y += Mathf.Sin(progress * Mathf.PI) * stepHeight;

            transform.position = currentPos;
            yield return null;
        }

        transform.position = targetPos;
        IsStepping = false;
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