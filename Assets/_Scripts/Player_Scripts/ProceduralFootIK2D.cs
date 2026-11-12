using UnityEngine;
using UnityEngine.U2D.IK; // Package resmi 2D IK Unity

public class ProceduralFootIK2D : MonoBehaviour
{
    [Header("Referensi Controller")]
    public PlatformerMovement movement;

    [Header("Komponen Solver Kaki (Dari Folder Solvers)")]
    public LimbSolver2D leftLegSolver;
    public LimbSolver2D rightLegSolver;

    [Header("Target Kaki (Dari Folder Targets)")]
    public Transform leftFootTarget;
    public Transform rightFootTarget;

    [Header("Pengaturan Deteksi Tangga / Lereng")]
    public LayerMask groundLayer;
    public float raycastDistance = 1.2f;
    public float footYOffset = 0.1f;
    public float weightBlendSpeed = 8f;

    void LateUpdate()
    {
        if (movement == null) return;

        bool ShouldApplyFootIK = movement.IsGrounded && !movement.IsMoving;
        float targetWeight = ShouldApplyFootIK? 1f : 0f;

        if (leftLegSolver != null)
            leftLegSolver.weight = Mathf.Lerp(leftLegSolver.weight, targetWeight, weightBlendSpeed * Time.deltaTime);
        if (rightLegSolver != null)
            rightLegSolver.weight = Mathf.Lerp(rightLegSolver.weight, targetWeight, weightBlendSpeed * Time.deltaTime);

        if (ShouldApplyFootIK)
        {
            AdjustFootTarget(leftFootTarget);
            AdjustFootTarget(rightFootTarget);
        }
    }

    void AdjustFootTarget(Transform footTarget)
    {
        if (footTarget == null) return;

        // Tembak laser 2D ke bawah dari atas posisi telapak kaki
        Vector2 rayOrigin = new Vector2(footTarget.position.x, footTarget.position.y * 0.5f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, raycastDistance + 0.5f, groundLayer);

        // Tempelkan target tepat di permukaan tanah/anak tangga
        if (hit.collider != null)
            footTarget.position = new Vector3(footTarget.position.x, hit.point.y + footYOffset, footTarget.position.z);
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