using UnityEngine;
using UnityEngine.U2D.IK;

public class ProceduralFootIK2D : MonoBehaviour
{
    [Header("Referensi Controller Platformer")]
    public PlatformerMovement movement;

    [Header("Komponen Solver Kaki")]
    public LimbSolver2D leftLegSolver;
    public LimbSolver2D rightLegSolver;

    [Header("Tulang Tubuh untuk Penyeimbang")]
    public Transform pelvisBone;
    public Transform spineBone;

    [Header("Kemiringan Tubuh (Body Lean)")]
    public float climbLeanAngle = 12f;

    [Header("Target Kaki")]
    public Transform leftFootTarget;
    public Transform rightFootTarget;

    [Header("Pengaturan Deteksi Tangga")]
    public LayerMask groundLayer;
    public float raycastDistance = 1.5f;
    public float footYOffset = 0.05f;
    public float adaptSpeed = 15f;

    private float defaultPelvisY;
    private Quaternion defaultSpineRot;
    private float leftOffset = 0f;
    private float rightOffset = 0f;

    void Start()
    {

        if (leftLegSolver != null) leftLegSolver.weight = 1f;
        if (rightLegSolver != null) rightLegSolver.weight = 1f;

        if (pelvisBone != null) defaultPelvisY = pelvisBone.localPosition.y;
        if (spineBone != null) defaultSpineRot = spineBone.localRotation;
    }

    void LateUpdate()
    {
        if (movement == null) return;

        float targetLeftOffset = CalculateFootOffset(leftFootTarget, out bool leftHit);
        float targetRightOffset = CalculateFootOffset(rightFootTarget, out bool rightHit);

        leftOffset = Mathf.Lerp(leftOffset, targetLeftOffset, adaptSpeed * Time.deltaTime);
        rightOffset = Mathf.Lerp(rightOffset, targetRightOffset, adaptSpeed * Time.deltaTime);

        if (leftFootTarget != null && leftHit)
            leftFootTarget.position += Vector3.up * leftOffset;

        if (rightFootTarget != null && rightHit)
            rightFootTarget.position += Vector3.up * rightOffset;

        float heightDif = Mathf.Abs(targetLeftOffset - targetRightOffset);
        bool onStair = heightDif > 0.05f && movement.IsGrounded;

        if (pelvisBone != null)
        {
            float targetPelvisY = defaultPelvisY;
            if (onStair)
                targetPelvisY = defaultPelvisY - (heightDif * 0.5f);

            float smoothPelvisY = Mathf.Lerp(pelvisBone.localPosition.x, targetPelvisY, adaptSpeed * Time.deltaTime);
            pelvisBone.localPosition = new Vector3(pelvisBone.localPosition.x, smoothPelvisY, pelvisBone.localPosition.z);
        }

        if (spineBone != null)
        {
            Quaternion targetLean = defaultSpineRot;
            if (onStair)
                targetLean = defaultSpineRot * Quaternion.Euler(0f, 0f, -climbLeanAngle);

            spineBone.localRotation = Quaternion.Slerp(spineBone.localRotation, targetLean, 8f * Time.deltaTime);
        }
    }

    float CalculateFootOffset(Transform footTarget, out bool hitsucces)
    {
        hitsucces = false;
        if (footTarget == null) return 0f;  

        Vector2 rayOrigin = new Vector2(footTarget.position.x, transform.position.y + 0.5f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, raycastDistance + 0.5f, groundLayer);

        if (hit.collider != null)
        {
            hitsucces = true;
            float groundHeight = hit.point.y + footYOffset;
            float characterBaseY = movement.transform.position.y;
            
            return (groundHeight - characterBaseY);
        }

        return 0f;
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