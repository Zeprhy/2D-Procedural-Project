using UnityEngine;
using UnityEngine.U2D.IK;

public class ProceduralFootIK2D : MonoBehaviour
{
    [Header("Referensi Controller Platformer")]
    public PlatformerMovement movement;

    [Header("Komponen Solver Kaki")]
    public LimbSolver2D leftLegSolver;
    public LimbSolver2D rightLegSolver;

    [Header("Target Kaki")]
    public Transform leftFootTarget;
    public Transform rightFootTarget;

    [Header("Pengaturan Deteksi Tangga / Lereng")]
    public LayerMask groundLayer;
    public float raycastDistance = 1.2f;
    public float footYOffset = 0.1f;
    public float blendInSpeed = 10f; // Kecepatan menempelkan kaki saat diam

    void LateUpdate()
    {
        if (movement == null) return;

        bool isIdleOnGround = movement.IsGrounded && !movement.IsMoving;

        if (isIdleOnGround)
        {
            if (leftLegSolver != null)
                leftLegSolver.weight = Mathf.Lerp(leftLegSolver.weight, 1f, blendInSpeed * Time.deltaTime);

            if (rightLegSolver != null)
                rightLegSolver.weight = Mathf.Lerp(rightLegSolver.weight, 1f, blendInSpeed * Time.deltaTime);

            AdjustFootTarget(leftFootTarget);
            AdjustFootTarget(rightFootTarget);
        }
        else
        {
            if (leftLegSolver != null) leftLegSolver.weight = 0f;
            if (rightLegSolver != null) rightLegSolver.weight = 0f;
        }
    }

    void AdjustFootTarget(Transform footTarget)
    {
        if (footTarget == null) return;

        Vector2 rayOrigin = new Vector2(footTarget.position.x, transform.position.y + 0.5f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, raycastDistance + 0.5f, groundLayer);

        if (hit.collider != null)
        {
            footTarget.position = new Vector3(footTarget.position.x, hit.point.y + footYOffset, footTarget.position.z);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (leftFootTarget != null)
            Gizmos.DrawLine(leftFootTarget.position + Vector3.up * 0.5f, leftFootTarget.position + Vector3.down * raycastDistance);
        if (rightFootTarget != null)
            Gizmos.DrawLine(rightFootTarget.position + Vector3.up * 0.5f, rightFootTarget.position + Vector3.down * raycastDistance);
    }
}