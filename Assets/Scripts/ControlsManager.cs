using UnityEngine;
using UnityEngine.InputSystem;

public class ControlsManager : MonoBehaviour
{
    private PlayerInput _playerInput;
    public InputAction ScrollAction { get; private set; }
    
    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        ScrollAction = _playerInput.actions["Scroll"];
    }
}
