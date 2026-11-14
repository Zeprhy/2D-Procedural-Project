using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform _target;

    [Header("Pengaturan Follow")]
    [SerializeField] private float _smoothTime = 0.2f;
    [SerializeField] private Vector3 _offset = new Vector3(0f, 1.5f, -10f);

    private Vector3 _currentVelocity = Vector3.zero;

    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 targetPosition = _target.position + _offset;
        
        // [Pasti] SmoothDamp di LateUpdate menjamin kamera tidak bergetar mengikuti Rigidbody
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, _smoothTime);
    }
}