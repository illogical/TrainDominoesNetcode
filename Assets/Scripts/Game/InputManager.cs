using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class InputManager : MonoBehaviour
{
    public event EventHandler<int> DominoClicked;
    public event EventHandler DrawButtonClicked;
    public event EventHandler EndTurnClicked;
    public event EventHandler ReadyButtonClicked;

    public Camera MainCamera;
    [Space]
    [SerializeField] private Button DrawButton;
    [SerializeField] private Button EndTurnButton;
    [SerializeField] private Button RoundReadyButton;
    [SerializeField] private Button RestartReadyButton;


    private void Start()
    {
        DrawButton.onClick.AddListener(OnDrawButtonClicked);
        EndTurnButton.onClick.AddListener(OnEndTurnButtonClicked);
        RoundReadyButton.onClick.AddListener(OnReadyButtonClicked);
        RestartReadyButton.onClick.AddListener(OnReadyButtonClicked);
    }

    void Update()
    {
        GetMouseClick();
    }

    private void OnDrawButtonClicked()
    {
        DrawButtonClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnEndTurnButtonClicked()
    {
        EndTurnClicked?.Invoke(this, EventArgs.Empty);
    }
    
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

    public void SetDrawButtonEnabled(bool enabled) => DrawButton.interactable = enabled;
    public void SetEndTurnButtonEnabled(bool enabled) => EndTurnButton.interactable = enabled;
    public void SetRoundReadyButtonEnabled(bool enabled) => RoundReadyButton.interactable = enabled;
    public void SetRestartReadyButtonEnabled(bool enabled) => RestartReadyButton.interactable = enabled;

}
