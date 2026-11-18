using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class HitVFXInstance : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Mengonfigurasi tampilan dan lifetime visual effect
    /// </summary>
    public void Setup(Sprite vfxSprite, Vector3 position, Quaternion rotation, Vector3 scale, float lifetime, string sortingLayerName, int sortingOrder)
    {
        transform.position = position;
        transform.rotation = rotation;
        transform.localScale = scale;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = vfxSprite;
            
            if (!string.IsNullOrEmpty(sortingLayerName))
                _spriteRenderer.sortingLayerName = sortingLayerName;

            _spriteRenderer.sortingOrder = sortingOrder;
        }

        StartCoroutine(LifetimeRoutine(lifetime));
    }

    private IEnumerator LifetimeRoutine(float duration)
    {
        // Gunakan Realtime agar VFX tetap membeku di layar selama Hit Stop berlangsung
        yield return new WaitForSecondsRealtime(duration);
        Destroy(gameObject);
    }
}