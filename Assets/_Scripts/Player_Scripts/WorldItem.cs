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
            return;
        
        _itemData = itemData;

        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer != null)
        {
            Sprite displaySprite = itemData.WorldItemSprite;
            if (displaySprite != null)
                _spriteRenderer.sprite = displaySprite;
        }
    }

    public void MarkAsConsumed()
    {
        IsConsumed = true;
        if (TryGetComponent<Collider2D>(out var col))
            col.enabled = false;
    }
}