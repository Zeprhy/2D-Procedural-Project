using UnityEngine;

[SelectionBase]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class WorldItem : MonoBehaviour
{
    [Header("Data Item")]
    [Tooltip("Data ScriptableObject senjata yang diwakili oleh World Item ini")]
    [SerializeField] private BaseWeaponSO _itemData;

    [Header("Komponen Visual")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    public BaseWeaponSO ItemData => _itemData;
    public bool IsConsumed {get; private set;}

    private void Awake()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_itemData != null)
            Initialize(_itemData);
    }

    public void Initialize(BaseWeaponSO itemData)
    {
        if (itemData == null)
        {
            Debug.LogWarning($"<color=yellow>[WorldItem]</color> Mencoba menginisialisasi WorldItem di '{gameObject.name}' dengan data null!", this);
            return;
        }
        
        _itemData = itemData;

        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer != null)
        {
            Sprite displaySprite = itemData.WorldItemSprite;
            if (displaySprite != null)
                _spriteRenderer.sprite = displaySprite;
            else    
            {
                Debug.LogWarning($"<color=yellow>[WorldItem]</color> Sprite untuk '{itemData.WeaponName}' belum diatur pada BaseWeaponSO!", this);
            }
        }
    }

    public void MarkAsConsumed()
    {
        IsConsumed = true;
        if (TryGetComponent<Collider2D>(out var col))
            col.enabled = false;
    }
}