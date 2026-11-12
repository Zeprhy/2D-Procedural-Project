using UnityEngine;
public class FlipToMousePos : MonoBehaviour
{
    private Camera _main;
    private bool _lookingRight;
    public bool LookingOnRight { get => _lookingRight; set => _lookingRight = value; }
    void Start()
    {
        _main = FindObjectOfType<Camera>();
    }
    private void Update()
    {
        if (Input.mousePosition.x < _main.WorldToScreenPoint(transform.position).x)
        {
            transform.localRotation = Quaternion.Euler(0, 180, 0);
            _lookingRight = false;
        }
        else
        {
            transform.localRotation = Quaternion.Euler(0, 0, 0);
            _lookingRight = true;
        }
    }
}
