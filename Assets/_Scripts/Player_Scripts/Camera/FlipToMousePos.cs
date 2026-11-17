using UnityEngine;

public class FlipToMousePos : MonoBehaviour
{
    [SerializeField] private float _flipDeadzone = 0.45f;

    private Camera _main;
    private bool _lookingRight = true;
    public bool LookingOnRight => _lookingRight;

    private void Start()
    {
        _main = Camera.main;
    }

    private void Update()
    {
        if (_main == null)
        {
            _main = Camera.main;
            return;
        }

        Vector3 mouseWorldPos = _main.ScreenToWorldPoint(Input.mousePosition);
        float diffX = mouseWorldPos.x - transform.position.x;

        if (_lookingRight)
        {
            if (diffX < -_flipDeadzone)
            {
                transform.localScale = new Vector3(-1f, 1f, 1f);
                _lookingRight = false;
            }
        }
        else
        {
            if (diffX > _flipDeadzone)
            {
                transform.localScale = new Vector3(1f, 1f, 1f);
                _lookingRight = true;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        float dir = _lookingRight ? 1f : -1f;

        // Garis Hijau = Safe Zone di depan badan
        Gizmos.color = Color.green;
        Vector3 center = transform.position;
        Gizmos.DrawLine(center, center + new Vector3(dir * 2.5f, 0f, 0f));

        // Garis Merah = Titik Pemicu Flip di belakang punggung
        Gizmos.color = Color.red;
        Vector3 flipThresholdPos = center - new Vector3(dir * _flipDeadzone, 0f, 0f);
        Gizmos.DrawWireCube(flipThresholdPos, new Vector3(0.08f, 0.8f, 0f));
    }
}