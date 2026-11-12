using UnityEngine;

public class PelvisController2D : MonoBehaviour
{
    [SerializeField] private Transform leftFootTarget;
    [SerializeField] private Transform rightFootTarget;
    [SerializeField] private float defaultHeightOffset = 1.3f;
    [SerializeField] private float smoothSpeed = 10f;

    private void LateUpdate()
    {
        if (leftFootTarget == null || rightFootTarget == null) return;

        float avarageFootY = (leftFootTarget.position.y + rightFootTarget.position.y) * 0.5f;

        // [Konsep Inti] Pinggul turun sedikit saat kedua kaki merenggang jauh (langkah terbuka)
        float feetSpread = Mathf.Abs(leftFootTarget.position.x - rightFootTarget.position.x);
        float bobOffset = feetSpread * 0.12f;// Semakin renggang, pinggul turun sedikit

        float targetY = avarageFootY + defaultHeightOffset - bobOffset;

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * smoothSpeed);
        transform.position = pos;
    }
}