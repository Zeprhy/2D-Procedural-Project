using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Input : MonoBehaviour
{
    private float _inputX;
    private float _inputY;
    public float InputX { get => _inputX; set => _inputX = value; }
    public float InputY { get => _inputY; set => _inputY = value; }
    void Update()
    {
        _inputX = Input.GetAxisRaw("Horizontal");
        _inputY = Input.GetAxisRaw("Vertical");
    }
}
