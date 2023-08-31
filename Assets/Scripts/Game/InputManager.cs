using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class InputManager : MonoBehaviour
{
    public event EventHandler<int> DominoClicked;
    public event EventHandler DrawButtonClicked;
    public event EventHandler EndTurnClicked;
    public event EventHandler ReadyButtonClicked;
    public event EventHandler NewGameButtonClicked;
    public event EventHandler<DominoScrollEventArgs> ScrollOverDomino;

    public class DominoScrollEventArgs : EventArgs
    {
        public Vector2 ScrollAmount;
        public int DominoId;
    }

    public Camera MainCamera;
    [Space]
    [SerializeField]
    internal ControlsManager ControlsManager;
    [Header("UI Buttons")]
    [SerializeField] private Button DrawButton;
    [SerializeField] private Button EndTurnButton;
    [SerializeField] private Button RoundReadyButton;
    [SerializeField] private Button RestartReadyButton;
    [SerializeField] private Button NewGameButton;
    [SerializeField] private Button QuitButton;

    private void Start()
    {
        DrawButton.onClick.AddListener(OnDrawButtonClicked);
        EndTurnButton.onClick.AddListener(OnEndTurnButtonClicked);
        RoundReadyButton.onClick.AddListener(OnReadyButtonClicked);
        RestartReadyButton.onClick.AddListener(OnReadyButtonClicked);
        NewGameButton.onClick.AddListener(OnNewGameButtonClicked);
        QuitButton.onClick.AddListener(() => Application.Quit());
        
        ControlsManager.ScrollAction.performed += ScrollActionOnperformed;
    }

    void Update()
    {
        GetMouseClick();
    }

    private void OnDestroy()
    {
        ControlsManager.ScrollAction.performed -= ScrollActionOnperformed;
    }
    
    private void ScrollActionOnperformed(InputAction.CallbackContext obj)
    {
        GetScrollTrack(obj.ReadValue<Vector2>());
    }

    private void OnDrawButtonClicked() => DrawButtonClicked?.Invoke(this, EventArgs.Empty);
    private void OnEndTurnButtonClicked() => EndTurnClicked?.Invoke(this, EventArgs.Empty);
    private void OnNewGameButtonClicked() => NewGameButtonClicked?.Invoke(this, EventArgs.Empty);
    
    private void OnReadyButtonClicked()
    {
        RoundReadyButton.interactable = false;
        RestartReadyButton.interactable = false;
        ReadyButtonClicked?.Invoke(this, EventArgs.Empty);
    }
    
    private void GetMouseClick()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        Ray ray = MainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            var dominoEntity = hit.transform.gameObject.GetComponent<DominoEntityUI>();
            DominoEntity dominoInfo = dominoEntity.DominoInfo;
            if (dominoInfo == null)
            {
                return;
            }

            MouseClickedObject(dominoInfo.ID, dominoInfo.Purpose);
        }

        void MouseClickedObject(int id, PurposeType purpose)
        {
            Debug.Log($"Domino {id} ({purpose}) was clicked via InputManager");

            DominoClicked?.Invoke(this, id);
        }
    }

    private void GetScrollTrack(Vector2 scrollAmount)
    {
        Ray ray = MainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            var dominoEntity = hit.transform.gameObject.GetComponent<DominoEntityUI>();
            DominoEntity dominoInfo = dominoEntity.DominoInfo;
            //if (dominoInfo is not { Purpose: PurposeType.Track })
            if (dominoInfo == null)
            {
                return;
            }
            
            ScrollOverDomino?.Invoke(this, new DominoScrollEventArgs() { ScrollAmount = scrollAmount, DominoId = dominoInfo.ID});
        }
    }

    public void SetDrawButtonEnabled(bool enabled) => DrawButton.interactable = enabled;
    public void SetEndTurnButtonEnabled(bool enabled) => EndTurnButton.interactable = enabled;
    public void SetRoundReadyButtonEnabled(bool enabled) => RoundReadyButton.interactable = enabled;
    public void SetRestartReadyButtonEnabled(bool enabled) => RestartReadyButton.interactable = enabled;

}
