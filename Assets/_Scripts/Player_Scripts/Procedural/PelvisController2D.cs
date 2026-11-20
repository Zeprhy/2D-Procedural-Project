using UnityEngine;

[DefaultExecutionOrder(-100)] 
public class PelvisController2D : MonoBehaviour
{
    [Header("Referensi Kaki")]
    [SerializeField] private Transform leftFootTarget;
    [SerializeField] private Transform rightFootTarget;

    [Header("Pengaturan Pinggul")]
    [SerializeField] private float defaultHeightOffset = 1.3f;
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float bobbingIntensity = 0.1f;

    [Header("Fix Bobbing Tajam (Biar Pedang Nggak Kepentok Tanah)")]
    [Tooltip("Batas maksimal tubuh turun saat bobbing (world unit)")]
    [SerializeField] private float maxBobOffset = 0.05f;
    [Tooltip("Waktu smoothing buka-tutup langkah. Makin besar = makin lembut")]
    [SerializeField] private float bobSmoothTime = 0.12f;

    private float _spreadSmooth;
    private float _spreadVel;
    private bool _initialized;


    private void LateUpdate()
    {
        if (leftFootTarget == null || rightFootTarget == null) return;

        // Ketinggian rata-rata kedua telapak kaki
        float averageFootY = (leftFootTarget.position.y + rightFootTarget.position.y) * 0.5f;

        // Bobbing: tubuh turun sedikit saat langkah terbuka lebar
        float feetSpread = Mathf.Abs(leftFootTarget.position.x - rightFootTarget.position.x);

        if (!_initialized)
        {
            _spreadSmooth = feetSpread;
            _initialized = true;
        }

        _spreadSmooth = Mathf.SmoothDamp(_spreadSmooth, feetSpread, ref _spreadVel, bobSmoothTime);
        float bobOffset = Mathf.Min(_spreadSmooth * bobbingIntensity, maxBobOffset);
        float targetY = averageFootY + defaultHeightOffset - bobOffset;

        Vector3 pos = transform.position;
        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        pos.y = Mathf.Lerp(pos.y, targetY, t);
        transform.position = pos;
    }
}