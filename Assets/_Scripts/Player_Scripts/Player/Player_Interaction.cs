using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInteraction : MonoBehaviour
{
    [Header("Pengaturan Deteksi")]
    [Tooltip("Radius lingkaran deteksi item di sekitar pemain")]
    [SerializeField] private float _interactionRadius = 1.5f;

    [Tooltip("LayerMask untuk menyaring objek WorldItem")]
    [SerializeField] private LayerMask _itemLayerMask;

    [Tooltip("Interval waktu pemindaian berkala (detik)")]
    [SerializeField] private float _scanInterval = 0.1f;

    [Tooltip("Kapasitas buffer array untuk pemindaian non-alloc")]
    [SerializeField] private int _bufferCapacity = 8;

    [Header("Input Interaksi")]
    [Tooltip("Tombol keyboard untuk mengambil / berinteraksi dengan item")]
    [SerializeField] private KeyCode _interactKey = KeyCode.E;

    private WorldItem _currentTarget;
    public WorldItem CurrentTarget => _currentTarget;
    private Collider2D[] _colliderBuffer;
    private float _scanTimer;

    private void Awake()
    {
        int capacity = Mathf.Max(1, _bufferCapacity);
        _colliderBuffer = new Collider2D[capacity];
    }

    private void Update()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = _scanInterval;
            ScanForItems();
        }

        if (_currentTarget != null && (!_currentTarget.gameObject.activeInHierarchy || _currentTarget.IsConsumed))
            _currentTarget = null;

        if (Input.GetKeyDown(_interactKey))
            TryInteractWithTarget();
    }

    private void TryInteractWithTarget()
    {
        if (_currentTarget == null || _currentTarget.IsConsumed) return;

        if (GameManager.Instance != null && GameManager.Instance.WeaponEquipmentManager != null)
        {
            bool success = GameManager.Instance.WeaponEquipmentManager.TryLootWeapon(_currentTarget);
            if (success)
                _currentTarget = null;
        }
        else
        {
            Debug.LogWarning("<color=yellow>[PlayerInteraction]</color> Akses gagal: GameManager atau WeaponEquipmentManager tidak tersedia!", this);
        }
    }

    private void ScanForItems()
    {
        Vector2 origin = transform.position;

        int hitCount = Physics2D.OverlapCircleNonAlloc(origin, _interactionRadius, _colliderBuffer, _itemLayerMask);
        WorldItem closestItem = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < hitCount ; i++)
        {
            Collider2D col = _colliderBuffer[i];
            if (col == null) continue;

            WorldItem item = col.GetComponent<WorldItem>();
            if (item == null)
                item = col.GetComponentInParent<WorldItem>();

            if (item == null || !item.isActiveAndEnabled || item.ItemData == null) continue;

            float sqrDist = ((Vector2)item.transform.position - origin).sqrMagnitude;
            if (sqrDist < closestSqrDistance)
            {
                closestSqrDistance = sqrDist;
                closestItem = item;
            }
        }

        _currentTarget = closestItem;
    }

     private void OnDrawGizmosSelected()
    {
        // Visualisasi radius interaksi di Scene View saat Player dipilih
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _interactionRadius);

        // Garis penunjuk ke target yang sedang aktif terpilih
        if (_currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
            Gizmos.DrawWireCube(_currentTarget.transform.position, Vector3.one * 0.3f);
        }
    }
}