using UnityEngine;

public class Player_Animator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlatformerMovement _playerMovement;
    [SerializeField] private Player_Input _playerInput;
    [SerializeField] private FlipToMousePos _flipToMouse;
    [SerializeField] private Animator _animator;

    private void Update()
    {
        if (_animator == null || _playerMovement == null) return;

        if (_animator.GetCurrentAnimatorStateInfo(0).IsTag("block"))
            _playerMovement.Freeze = true;
        else 
            _playerMovement.Freeze = false;  

        bool isMoving = Mathf.Abs(_playerMovement.PlayerRigidbody.linearVelocity.x) > 0.2f;
        _animator.SetBool("isMoving", isMoving);

        bool isBackward = (_playerInput.InputX < 0 && _flipToMouse.LookingOnRight) || 
                          (_playerInput.InputX > 0 && !_flipToMouse.LookingOnRight);
        _animator.SetBool("isBackward", isBackward);

        _animator.SetBool("isGrounded", _playerMovement.IsGrounded);
    }
}