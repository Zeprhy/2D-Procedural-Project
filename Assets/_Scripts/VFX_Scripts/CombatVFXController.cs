using UnityEngine;

public class CombatVFXController : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab reusable yang memiliki komponen HitVFXInstance dan SpriteRenderer")]
    [SerializeField] private HitVFXInstance _vfxPrefab;

    [Header("Sorting Layer")]
    [SerializeField] private string _sortingLayerName = "Default";
    [SerializeField] private int _sortingOrder = 100;

    [Header("Normal Hit (Directional Slash)")]
    [Tooltip("Variasi sprite tebasan tajam (hitam/putih directional)")]
    [SerializeField] private Sprite[] _normalHitSprites;
    [SerializeField] private Vector3 _normalScale = new Vector3(1f, 1f, 1f);
    [SerializeField] private float _normalLifetime = 0.07f;
    [SerializeField] private Vector2 _normalOffset = Vector2.zero;

    [Header("Critical Hit (Colorful Impact Star)")]
    [Tooltip("Variasi sprite ledakan bintang komik warna-warni")]
    [SerializeField] private Sprite[] _criticalHitSprites;
    [SerializeField] private Vector3 _criticalScale = new Vector3(1.5f, 1.5f, 1f);
    [SerializeField] private float _criticalLifetime = 0.11f;
    [SerializeField] private Vector2 _criticalOffset = Vector2.zero;
    [SerializeField] private bool _randomizeCriticalRotation = true;

    /// <summary>
    /// Memutar efek tebasan terarah (Normal Hit)
    /// </summary>
    public void PlayNormalHit(Vector3 hitPoint, Vector2 hitDirection)
    {
        if (_vfxPrefab == null || _normalHitSprites == null || _normalHitSprites.Length == 0) return;

        Sprite selectedSprite = _normalHitSprites[Random.Range(0, _normalHitSprites.Length)];
        if (selectedSprite == null) return;

        float angle = Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        Vector3 spawnPos = hitPoint + (Vector3)_normalOffset;
        HitVFXInstance instance = Instantiate(_vfxPrefab);
        instance.Setup(selectedSprite, spawnPos, rotation, _normalScale, _normalLifetime, _sortingLayerName, _sortingOrder);
    }


    /// <summary>
    /// Memutar efek impact bintang komik (Critical Hit)
    /// </summary>
    public void PlayCriticalHit(Vector3 hitPoint, Vector2 hitDirection)
    {
        if (_vfxPrefab == null || _normalHitSprites == null || _normalHitSprites.Length == 0) return;

        Sprite selectedSprite = _criticalHitSprites[Random.Range(0, _criticalHitSprites.Length)];
        if (selectedSprite == null) return;

        Quaternion rotation = Quaternion.identity;
        if (_randomizeCriticalRotation)
            rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        else
        {
            float angle = Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg;
            rotation = Quaternion.Euler(0f, 0f, angle);
        }

        Vector3 spawnPos = hitPoint + (Vector3)_criticalOffset;
        HitVFXInstance instance = Instantiate(_vfxPrefab);
        instance.Setup(selectedSprite, spawnPos, rotation, _criticalScale, _criticalLifetime, _sortingLayerName, _sortingOrder);
    }
}