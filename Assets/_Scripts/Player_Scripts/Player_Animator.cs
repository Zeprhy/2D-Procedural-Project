using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Player_Animator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TopDownMovement _playerMovement;
    [SerializeField] private Player_Input _playerInput;
    [SerializeField] private FlipToMousePos _flipToMouse;
    [SerializeField] private Transform _gfx;
    [Header("Components")]
    [SerializeField] private Animator _animator;
    public Animator playerAnimator { get => _animator; set => _animator = value; }
    private void Update()
    {
        Animations();
    }
    public void Animations()
    {
        if (_animator.GetCurrentAnimatorStateInfo(0).IsTag("block"))
            _playerMovement.Freeze = true;
        else _playerMovement.Freeze = false;  
        
        if (_playerMovement.PlayerRigidbody.linearVelocity.magnitude > 0.5f)
        {
            _animator.SetBool("isMoving", true);
        }
        else
        {
            _animator.SetBool("isMoving", false);
        }
        if(_playerInput.InputX < 0 && _flipToMouse.LookingOnRight || _playerInput.InputX > 0 && !_flipToMouse.LookingOnRight)
        {
            _animator.SetBool("isBackward", true);
        }
        else _animator.SetBool("isBackward", false);
    }
}
