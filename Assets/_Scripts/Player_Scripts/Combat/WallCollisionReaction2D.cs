using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WallCollisionReaction2D : MonoBehaviour
{
    [Header("Penyaringan Tabrakan (Collision Filtering)")]
    [Tooltip("LayerMask yang menandai GameObject Dinding")]
    [SerializeField] private LayerMask _wallLayer;
    [Tooltip("Kecepatan tabrakan minimum ke arah dinding untuk memicu reaksi")]
    [SerializeField] private float _minImpactSpeed = 4.5f;
    [Tooltip("Nilai absolut normal horizontal minimum untuk mengidentifikasi dinding vertikal (mencegah salah deteksi lantai)")]
    [Range(0.5f, 0.95f)]
    [SerializeField] private float _minWallNormalX = 0.65f;

    [Header("Fisika Pantulan (Rebound Physics)")]
    [Tooltip("Pengali kekuatan dorongan memantul dari dinding")]
    [SerializeField] private float _reboundStrength = 0.65f;
    [Tooltip("Peredaman kecepatan tangensial (meluncur di sepanjang dinding)")]
    [Range(0f, 1f)]
    [SerializeField] private float _tangentialDamping = 0.75f;
    [Tooltip("Apakah pantulan menyertakan sedikit gaya angkat ke atas agar tidak terhambat gesekan lantai")]
    [SerializeField] private bool _useUpwardLift = true;
    [Tooltip("Besar gaya angkat vertikal saat memantul")]
    [SerializeField] private float _upwardLift = 2.0f;
    [Tooltip("Waktu jeda antar pantulan untuk mencegah osilasi bolak-balik tanpa henti (ping-pong)")]
    [SerializeField] private float _reboundCooldown = 0.25f;

    [Header("Reaksi Visual (Wall Squash)")]
    [SerializeField] private bool _triggerVisualSquash = true;
    [Tooltip("Kompresi sumbu X saat menabrak dinding (0.75 = memipih 25%)")]
    [Range(0.5f, 0.95f)]
    [SerializeField] private float _wallSquashXMultiplier = 0.75f;
    [SerializeField] private float _wallSquashDuration = 0.04f;
    [SerializeField] private float _wallRecoverDuration = 0.10f;

    [Header("Visual Effects (VFX)")]
    [Tooltip("Prefab reusable HitVFXInstance yang sudah ada di project")]
    [SerializeField] private HitVFXInstance _vfxPrefab;
    [Tooltip("Variasi sprite efek benturan dinding bergaya manga")]
    [SerializeField] private Sprite[] _wallImpactSprites;
    [SerializeField] private Vector3 _vfxScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField] private float _vfxLifetime = 0.08f;
    [SerializeField] private string _sortingLayerName = "Default";
    [SerializeField] private int _sortingOrder = 100;

    private Rigidbody2D _rb;
    private DummyEntity _dummyEntity;
    private Vector2 _preCollisionVelocity;
    private float _lastReboundTime = -999f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _dummyEntity = GetComponent<DummyEntity>();
    }

    private void FixedUpdate()
    {
        _rb = _rb != null ? _rb : GetComponent<Rigidbody2D>();
        _preCollisionVelocity = _rb.linearVelocity;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time < _lastReboundTime + _reboundCooldown) return;

        if (((1 << collision.gameObject.layer) & _wallLayer.value ) == 0) return;

        if (collision.contactCount == 0) return;
        ContactPoint2D contact = collision.GetContact(0);

        Vector2 wallNormal = contact.normal;
        if (Mathf.Abs(wallNormal.x) < _minWallNormalX) return;

        float closingSpeed = -Vector2.Dot(_preCollisionVelocity, wallNormal);
        if (closingSpeed < _minImpactSpeed) return;

        _lastReboundTime = Time.time;
        ApplyReboundPhysics(wallNormal, closingSpeed);

        if (_triggerVisualSquash && _dummyEntity != null)
            _dummyEntity.TriggerWallImpactSquash(_wallSquashXMultiplier, _wallSquashDuration, _wallRecoverDuration);

        SpawnImpactVFX(contact.point, wallNormal);
    }

    private void ApplyReboundPhysics(Vector2 wallNormal , float closingSpeed)
    {
        Vector2 normalReboundvel = wallNormal * (closingSpeed * _reboundStrength);

        Vector2 tangent = new Vector2(-wallNormal.y, wallNormal.x);
        float tangentSpeed = Vector2.Dot(_preCollisionVelocity, tangent);
        Vector2 tangentiaAlvel = tangent * (tangentSpeed * _tangentialDamping);

        Vector2 finalVelocity = normalReboundvel + tangentiaAlvel;

        if (_useUpwardLift && _upwardLift > 0f)
            finalVelocity.y = Mathf.Max(finalVelocity.y, _upwardLift);
        
        _rb.linearVelocity = finalVelocity;
    }
    private void SpawnImpactVFX(Vector2 contactPoint, Vector2 wallNormal)
    {
        if (_vfxPrefab == null || _wallImpactSprites == null || _wallImpactSprites.Length == 0 ) return;

        Sprite selectedSprite = _wallImpactSprites[Random.Range(0, _wallImpactSprites.Length)];
        if (selectedSprite == null) return;

        float angle = Mathf.Atan2(wallNormal.y, wallNormal.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        HitVFXInstance instance = Instantiate(_vfxPrefab);
        instance.Setup(selectedSprite, contactPoint, rotation, _vfxScale, _vfxLifetime, _sortingLayerName, _sortingOrder);
    }

}