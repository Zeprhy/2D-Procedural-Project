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

    [Header("Fix Bobbing Tajam")]
    [Tooltip("Batas maksimal tubuh turun saat bobbing (world unit)")]
    [SerializeField] private float maxBobOffset = 0.05f;
    [Tooltip("Waktu smoothing buka-tutup langkah. Makin besar = makin lembut")]
    [SerializeField] private float bobSmoothTime = 0.12f;

    [Header("Kompensasi Tinggi Idle (Root Cause Lutut Menekuk)")]
    [Tooltip("Tinggi tambahan pelvis SAAT karakter settled idle, mengurangi lipatan IK. 0 = nonaktif.")]
    [SerializeField] private float idleHeightBonus = 0.08f;

    private float _spreadSmooth;
    private float _spreadVel;
    private bool _initialized;

    private Vector3 _prevLeftFoot;
    private Vector3 _prevRightFoot;
    private bool _footPrevInit;
    private float _currentIdleBonus;

    private void LateUpdate()
    {
        if (leftFootTarget == null || rightFootTarget == null) return;

        float averageFootY = (leftFootTarget.position.y + rightFootTarget.position.y) * 0.5f;

        float feetSpread = Mathf.Abs(leftFootTarget.position.x - rightFootTarget.position.x);
        if (!_initialized)
        {
            _spreadSmooth = feetSpread;
            _initialized = true;
        }
        _spreadSmooth = Mathf.SmoothDamp(_spreadSmooth, feetSpread, ref _spreadVel, bobSmoothTime);
        float bobOffset = Mathf.Min(_spreadSmooth * bobbingIntensity, maxBobOffset);

        // (B) deteksi settled idle MANDIRI dari kecepatan kedua target kaki (tanpa referensi baru)
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float feetSpeed = 0f;
        if (_footPrevInit)
        {
            feetSpeed = (Vector3.Distance(leftFootTarget.position, _prevLeftFoot)
                       + Vector3.Distance(rightFootTarget.position, _prevRightFoot)) / dt;
        }
        _prevLeftFoot = leftFootTarget.position;
        _prevRightFoot = rightFootTarget.position;
        _footPrevInit = true;

        bool settled = feetSpeed < 0.02f;
        // ramp halus agar tidak ada pop saat transisi idle<->walk
        _currentIdleBonus = Mathf.MoveTowards(_currentIdleBonus, settled ? idleHeightBonus : 0f, 0.5f * dt);

        float targetY = averageFootY + defaultHeightOffset - bobOffset + _currentIdleBonus;

        Vector3 pos = transform.position;
        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        pos.y = Mathf.Lerp(pos.y, targetY, t);
        transform.position = pos;
    }
}