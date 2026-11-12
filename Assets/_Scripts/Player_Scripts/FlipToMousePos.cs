using UnityEngine;
public class FlipToMousePos : MonoBehaviour
{
    private Camera _main;
    private bool _lookingRight;
    public bool LookingOnRight => _lookingRight;
    void Start()
    {
        _main = FindObjectOfType<Camera>();
    }
    private void Update()
    {
        if (_main == null) return;

        float mouseScreenX = Input.mousePosition.x;
        float charScreenX = _main.WorldToScreenPoint(transform.position).x;

        if (mouseScreenX < charScreenX)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
            _lookingRight = false;
        }
        else
        {
            // Menghadap ke Kanan
            transform.localScale = new Vector3(1f, 1f, 1f);
            _lookingRight = true;
        }
    }
}
