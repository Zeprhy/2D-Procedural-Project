using UnityEngine;
using _Scripts.Interface;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnergyProjectile2D : MonoBehaviour
{
    [Header("Pengaturan Proyektil (Default / Fallback)")]
    [SerializeField] private float _speed = 22f;
    [SerializeField] private float _damage = 18f;
    [SerializeField] private float _lifetime = 2.5f;
    [SerializeField] private float _knockbackForce = 3.5f;

    [Header("Layer Penyaringan Tabrakan")]
    [Tooltip("Layer musuh yang menerima damage (IDamageable)")]
    [SerializeField] private LayerMask _targetLayer;
    [Tooltip("Layer rintangan lingkungan (Ground & Wall) yang menghancurkan peluru")]
    [SerializeField] private LayerMask _obstacleLayer;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private GameObject _owner;
    private bool _hasHit = false;
    private float _lifeTimer;

    private void Awake()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
        if (_col == null)
            _col = GetComponent<Collider2D>();

        _col.isTrigger = true;

        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= _lifetime)
            Despawn();
    }

    /// <summary>
    /// Diinisialisasi saat peluru ditembakkan dari GunLogic (Sub-Stage B3)
    /// </summary>
    public void Initialize(Vector2 direction, float damage, float speed, float lifetime, LayerMask targetLayer, LayerMask obstacleLayer, GameObject owner)
    {
        _damage = damage;
        _speed = speed;
        _lifetime = lifetime;
        _targetLayer = targetLayer;
        _obstacleLayer = obstacleLayer;
        _owner = owner;
        _hasHit = false;
        _lifeTimer = 0f;

        if (_owner != null)
        {
            Collider2D[] ownerColliders = _owner.GetComponentsInChildren<Collider2D>();
            for ( int i = 0; i < ownerColliders.Length; i++)
            {
                if (ownerColliders[i] != null && !ownerColliders[i].isTrigger)
                    Physics2D.IgnoreCollision(_col, ownerColliders[i], true);
            }
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        _rb.linearVelocity = direction.normalized * _speed;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_hasHit) return;
        if (_owner != null && (collision.gameObject == _owner || collision.transform.IsChildOf(_owner.transform)))
            return;
        if (collision.GetComponent<EnergyProjectile2D>() != null)
            return;

        if (((1 << collision.gameObject.layer) & _targetLayer.value) != 0)
        {
            IDamageable damageable = collision.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                _hasHit = true;
                
                Vector2 knockbackDir = _rb.linearVelocity.sqrMagnitude > 0.001f ? _rb.linearVelocity.normalized : (Vector2)transform.right;
                Vector2 finalKnockback = knockbackDir * _knockbackForce;

                damageable.TakeDamage(_damage, finalKnockback);

                Despawn();
                return;
            }
        }

        if (((1 << collision.gameObject.layer) & _obstacleLayer.value) != 0)
        {
            _hasHit = true;
            Despawn();
            return;
        }
    }

    private void Despawn()
    {
        _hasHit = true;
        _rb.linearVelocity = Vector2.zero;
        Destroy(gameObject);
    }
}