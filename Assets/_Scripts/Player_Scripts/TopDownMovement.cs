using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class TopDownMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player_Input _playerInput;
    [SerializeField] private Rigidbody2D _rigidbody;
    [Header("Player Settings")]
    [SerializeField] private float _speed;
    public Rigidbody2D PlayerRigidbody { get => _rigidbody; set => _rigidbody = value; }
    public float Speed { get => _speed; set => _speed = value; }
    public float MaxSpeed { get => _maxSpeed; set => _maxSpeed = value; }
    public bool Freeze { get; set; }
    private float _maxSpeed;
    private Vector2 _moveDirection;
    private void Start()
    {
        _maxSpeed = _speed;
    }
    private void Update()
    {
        if (Freeze)
            return;
        _moveDirection = transform.up * _playerInput.InputY + transform.right * _playerInput.InputX;
    }
    private void FixedUpdate()
    {
        _rigidbody.AddForce(_moveDirection.normalized * _speed * 10f, ForceMode2D.Force);
        SpeedControl();
    }
    private void SpeedControl()
    {
        Vector2 flatVelocity = new Vector2(_rigidbody.linearVelocity.x, _rigidbody.linearVelocity.y);
        if (flatVelocity.magnitude > _speed)
        {
            Vector2 limitVelocity = flatVelocity.normalized * _speed;
            _rigidbody.linearVelocity = new Vector2(limitVelocity.x, limitVelocity.y);
        }
    }
}
