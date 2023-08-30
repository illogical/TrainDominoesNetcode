using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class ControlsManager : MonoBehaviour
{
    private PlayerInput _playerInput;
    public InputAction ScrollAction { get; private set; }
    
    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        ScrollAction = _playerInput.actions["Scroll"];
    }

    private void OnEnable()
    {
        ScrollAction.performed += ScrollActionOnperformed;
    }

    private void OnDisable()
    {
        ScrollAction.performed -= ScrollActionOnperformed;
    }

    private void ScrollActionOnperformed(InputAction.CallbackContext obj)
    {
        // TODO: detect when we are over a domino that is on a track
        //Debug.Log($"Scrolling... {obj.ReadValue<Vector2>()}");
    }
}
