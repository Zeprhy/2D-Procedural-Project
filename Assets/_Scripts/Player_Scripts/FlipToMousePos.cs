using UnityEngine;

public class FlipToMousePos : MonoBehaviour
{
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

        float mouseScreenX = Input.mousePosition.x;
        float charScreenX = _main.WorldToScreenPoint(transform.position).x;

        if (mouseScreenX < charScreenX)
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