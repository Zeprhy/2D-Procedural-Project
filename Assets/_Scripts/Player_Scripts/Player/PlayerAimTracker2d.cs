using UnityEngine;

public class PlayerAimTracker2D : MonoBehaviour
{
    [Header("Input Mouse")]
    [SerializeField] private int _rightArmMouseButton = 0;
    [SerializeField] private int _leftArmMouseButton = 1;

    public bool IsAimingRight { get; private set; }
    public bool IsAimingLeft { get; private set; }
    public Vector3 MouseWorldPosition { get; private set; }

    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        if (_cam == null) return;

        Vector3 mouseScreen = Input.mousePosition;
        Vector3 worldPos = _cam.ScreenToWorldPoint(mouseScreen);
        worldPos.z = 0f;
        MouseWorldPosition = worldPos;

        IsAimingRight = Input.GetMouseButton(_rightArmMouseButton);
        IsAimingLeft = Input.GetMouseButton(_leftArmMouseButton);
    }
}