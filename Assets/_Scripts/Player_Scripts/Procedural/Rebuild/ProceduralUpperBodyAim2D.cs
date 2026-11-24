using UnityEngine;

/// <summary>
/// Procedural upper-body aim (head + spine chain) for 2D side-scrollers.
/// Head/spine follow the cursor, hard-clamped to +/- _maxLookAngle, mirror-safe and smoothed
/// via the existing ProceduralArmMath2DRebuild.UpdateAimAngle helper.
/// Does NOT own B_Body_Main (PelvisController2DRebuild) and does NOT read weapon overrides.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Procedural Animation/Procedural Upper Body Aim 2D")]
public class ProceduralUpperBodyAim2D : MonoBehaviour
{
    [Header("Bones (spine bottom -> top)")]
    [SerializeField] private Transform _headBone;                       // isi: S_Head
    [SerializeField] private Transform[] _spineBones;                   // urutan bawah->atas: B_Body_Middle, B_Body_Knees
    [SerializeField] private float _headWeight = 0.5f;
    [SerializeField] private float[] _spineWeights = new float[] { 0.2f, 0.3f }; // paralel index dengan _spineBones

    [Header("References")]
    [SerializeField] private Transform _gfxTransform;
    [SerializeField] private PlayerAimTracker2D _aimTracker;

    [Header("Tuning")]
    [SerializeField] private float _maxLookAngle = 90f;
    [SerializeField] private float _aimLagTime = 0.1f;
    [SerializeField] private bool _enableUpperBodyAim = true;

    // --- Runtime state ---
    private Quaternion _bindHeadRot = Quaternion.identity;
    private Quaternion[] _bindSpineRot;
    private int _spineCount;
    private float _smoothedAngle;
    private float _angleVelocity;
    private bool _warnedWeightMismatch;

    private void Awake()
    {
        if (_aimTracker == null)
            _aimTracker = FindFirstObjectByType<PlayerAimTracker2D>();

        if (_gfxTransform == null)
        {
            Transform root = transform.root;
            if (root != null)
            {
                Transform gfx = root.Find("GFX");
                if (gfx != null) _gfxTransform = gfx;
            }
        }

        CacheBindPose();
    }

    private void CacheBindPose()
    {
        _bindHeadRot = _headBone != null ? _headBone.localRotation : Quaternion.identity;

        int boneLen = _spineBones != null ? _spineBones.Length : 0;
        int weightLen = _spineWeights != null ? _spineWeights.Length : 0;

        if (boneLen != weightLen && !_warnedWeightMismatch)
        {
            Debug.LogWarning(
                $"[ProceduralUpperBodyAim2D] _spineWeights length ({weightLen}) != _spineBones length ({boneLen}). " +
                "Using minimum length.", this);
            _warnedWeightMismatch = true;
        }

        _spineCount = Mathf.Min(boneLen, weightLen);
        _bindSpineRot = new Quaternion[_spineCount];

        for (int i = 0; i < _spineCount; i++)
        {
            Transform b = _spineBones[i];
            _bindSpineRot[i] = b != null ? b.localRotation : Quaternion.identity;
        }
    }

    private void LateUpdate()
    {
        if (!_enableUpperBodyAim) return;
        if (_aimTracker == null || _gfxTransform == null) return;
        if (_headBone == null && _spineCount == 0) return;

        Transform neckPivot = GetNeckPivot();
        if (neckPivot == null) return;

        // WORLD-space target, helper converts to local-space via facingDir internally.
        Vector2 toMouse = (Vector2)_aimTracker.MouseWorldPosition - (Vector2)neckPivot.position;

        float facingDir = Mathf.Sign(_gfxTransform.localScale.x);
        if (Mathf.Approximately(facingDir, 0f)) facingDir = 1f;

        var cfg = new ProceduralArmMath2DRebuild.AimLagConfig(-_maxLookAngle, _maxLookAngle, _aimLagTime);
        _smoothedAngle = ProceduralArmMath2DRebuild.UpdateAimAngle(
            toMouse, facingDir, cfg, ref _angleVelocity, _smoothedAngle);

        // MIRROR-SAFE bind-pose application. Do NOT multiply _smoothedAngle by facingDir here.
        for (int i = 0; i < _spineCount; i++)
        {
            Transform bone = _spineBones[i];
            if (bone == null) continue;
            bone.localRotation = _bindSpineRot[i] *
                Quaternion.Euler(0f, 0f, _smoothedAngle * _spineWeights[i]);
        }

        if (_headBone != null)
        {
            _headBone.localRotation = _bindHeadRot *
                Quaternion.Euler(0f, 0f, _smoothedAngle * _headWeight);
        }
    }

    private Transform GetNeckPivot()
    {
        if (_spineBones != null && _spineBones.Length > 0)
        {
            Transform last = _spineBones[_spineBones.Length - 1];
            if (last != null) return last;
        }
        return _headBone;
    }

    private void OnDisable()
    {
        if (_headBone != null)
            _headBone.localRotation = _bindHeadRot;

        if (_spineBones != null && _bindSpineRot != null)
        {
            int n = Mathf.Min(_spineCount, _bindSpineRot.Length);
            for (int i = 0; i < n; i++)
            {
                if (_spineBones[i] != null)
                    _spineBones[i].localRotation = _bindSpineRot[i];
            }
        }

        _smoothedAngle = 0f;
        _angleVelocity = 0f;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !_enableUpperBodyAim) return;

        Transform neckPivot = GetNeckPivot();
        if (neckPivot == null) return;

        float facingDir = (_gfxTransform != null) ? Mathf.Sign(_gfxTransform.localScale.x) : 1f;
        if (Mathf.Approximately(facingDir, 0f)) facingDir = 1f;

        float rad = _smoothedAngle * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(rad) * facingDir, Mathf.Sin(rad), 0f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(neckPivot.position, neckPivot.position + dir * 0.6f);
    }
}
