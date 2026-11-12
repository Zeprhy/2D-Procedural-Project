using UnityEngine;

public class PelvisController2D : MonoBehaviour
{
    [Header("Referensi Kaki")]
    [SerializeField] private Transform leftFootTarget;
    [SerializeField] private Transform rightFootTarget;

    [Header("Pengaturan Pinggul")]
    [SerializeField] private float defaultHeightOffset = 1.3f;
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float bobbingIntensity = 0.1f;

    private void LateUpdate()
    {
        if (leftFootTarget == null || rightFootTarget == null) return;

        // Ketinggian rata-rata kedua telapak kaki
        float averageFootY = (leftFootTarget.position.y + rightFootTarget.position.y) * 0.5f;

        // Bobbing: tubuh turun sedikit saat langkah terbuka lebar
        float feetSpread = Mathf.Abs(leftFootTarget.position.x - rightFootTarget.position.x);
        float bobOffset = feetSpread * bobbingIntensity;

        float targetY = averageFootY + defaultHeightOffset - bobOffset;

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * smoothSpeed);
        transform.position = pos;
    }
}