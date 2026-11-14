using UnityEngine;

public class Player_Input : MonoBehaviour
{
    private float _inputX;
    private float _inputY;

    public float InputX => _inputX;
    public float InputY => _inputY;

    private void Update()
    {
        _inputX = Input.GetAxisRaw("Horizontal");
        _inputY = Input.GetAxisRaw("Vertical");
    }
}