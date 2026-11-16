using UnityEngine;

public class FlipToMousePos : MonoBehaviour
{
    [SerializeField] private float _flipDeadzone = 0.35f;

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

        if (diffX < -_flipDeadzone)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
            _lookingRight = false;
        }
        else
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
            _lookingRight = true;
        }
    }
}