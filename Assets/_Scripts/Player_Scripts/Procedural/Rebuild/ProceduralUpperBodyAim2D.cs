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

    [Header("Auto Helpers")]
    [Tooltip("Urutkan otomatis _spineBones berdasarkan kedalaman hierarchy (bawah -> atas). Weight ikut urutannya.")]
    [SerializeField] private bool _autoSortSpine = true;
    [Tooltip("Normalisasi total porsi (spine + head) agar berjumlah 1.0, sehingga total look = sudut penuh.")]
    [SerializeField] private bool _normalizeWeights = true;

    [Header("References")]
    [SerializeField] private Transform _gfxTransform;
    [SerializeField] private PlayerAimTracker2D _aimTracker;

    [Header("Tuning")]
    [SerializeField] private float _maxLookAngle = 90f;
    [SerializeField] private float _aimLagTime = 0.1f;
    [SerializeField] private bool _enableUpperBodyAim = true;

    // --- Runtime state ---
    private Transform[] _spineBonesRuntime;
    private float[] _spineWeightsRuntime;
    private Quaternion[] _bindSpineRot;
    private int _spineCount;
    private float _effectiveHeadWeight;
    private Quaternion _bindHeadRot = Quaternion.identity;
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

        BuildRuntimeBoneData();
    }

    /// <summary>
    /// Builds runtime spine arrays: filters nulls, optionally sorts by hierarchy depth,
    /// optionally normalizes weights (spine + head) to sum 1.0, then caches bind rotations.
    /// </summary>
    private void BuildRuntimeBoneData()
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

        int count = Mathf.Min(boneLen, weightLen);

        // 1. Collect non-null pairs into scratch arrays (preserve raw order first).
        Transform[] bones = new Transform[count];
        float[] weights = new float[count];
        int valid = 0;
        for (int i = 0; i < count; i++)
        {
            Transform b = _spineBones[i];
            if (b == null) continue;
            bones[valid] = b;
            weights[valid] = _spineWeights[i];
            valid++;
        }

        // 2. Optional: sort bottom -> top by hierarchy depth (stable insertion sort, keeps weights parallel).
        if (_autoSortSpine && valid > 1)
        {
            int[] depths = new int[valid];
            for (int i = 0; i < valid; i++)
                depths[i] = GetHierarchyDepth(bones[i]);

            for (int i = 1; i < valid; i++)
            {
                Transform bone = bones[i];
                float weight = weights[i];
                int depth = depths[i];
                int j = i - 1;
                while (j >= 0 && depths[j] > depth)
                {
                    bones[j + 1] = bones[j];
                    weights[j + 1] = weights[j];
                    depths[j + 1] = depths[j];
                    j--;
                }
                bones[j + 1] = bone;
                weights[j + 1] = weight;
                depths[j + 1] = depth;
            }
        }

        // 3. Trim scratch arrays to valid length.
        _spineCount = valid;
        _spineBonesRuntime = new Transform[valid];
        _spineWeightsRuntime = new float[valid];
        _bindSpineRot = new Quaternion[valid];

        _effectiveHeadWeight = _headWeight;

        // 4. Optional: normalize (spine weights + head weight) to sum 1.0.
        if (_normalizeWeights)
        {
            float sum = 0f;
            for (int i = 0; i < valid; i++) sum += weights[i];
            if (_headBone != null) sum += _headWeight;

            if (sum > 0.0001f)
            {
                float inv = 1f / sum;
                for (int i = 0; i < valid; i++) weights[i] *= inv;
                _effectiveHeadWeight = _headWeight * inv;
            }
        }

        // 5. Commit + cache bind rotations.
        for (int i = 0; i < valid; i++)
        {
            _spineBonesRuntime[i] = bones[i];
            _spineWeightsRuntime[i] = weights[i];
            _bindSpineRot[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
        }
    }

    private static int GetHierarchyDepth(Transform t)
    {
        int depth = 0;
        Transform p = t != null ? t.parent : null;
        while (p != null)
        {
            depth++;
            p = p.parent;
        }
        return depth;
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
            Transform bone = _spineBonesRuntime[i];
            if (bone == null) continue;
            bone.localRotation = _bindSpineRot[i] *
                Quaternion.Euler(0f, 0f, _smoothedAngle * _spineWeightsRuntime[i]);
        }

        if (_headBone != null)
        {
            _headBone.localRotation = _bindHeadRot *
                Quaternion.Euler(0f, 0f, _smoothedAngle * _effectiveHeadWeight);
        }
    }

    private Transform GetNeckPivot()
    {
        if (_spineBonesRuntime != null && _spineCount > 0)
        {
            Transform last = _spineBonesRuntime[_spineCount - 1];
            if (last != null) return last;
        }
        return _headBone;
    }

    private void OnDisable()
    {
        if (_headBone != null)
            _headBone.localRotation = _bindHeadRot;

        if (_spineBonesRuntime != null && _bindSpineRot != null)
        {
            int n = Mathf.Min(_spineCount, _bindSpineRot.Length);
            for (int i = 0; i < n; i++)
            {
                if (_spineBonesRuntime[i] != null)
                    _spineBonesRuntime[i].localRotation = _bindSpineRot[i];
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
