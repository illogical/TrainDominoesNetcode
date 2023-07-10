using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class PanelRightUI : MonoBehaviour
{
    [SerializeField] public Button DrawButton;
    [SerializeField] public Button EndTurnButton;


    private void Start()
    {
        DrawButton.onClick.AddListener(OnDrawButtonClicked);
        EndTurnButton.onClick.AddListener(OnEndTurnButtonClicked);

        GameSession.Instance.OnPlayerJoined += OnPlayerJoined;

        DisableButtons();
    }

    private void OnDrawButtonClicked()
    {
        GameSession.Instance.DrawDominoesServerRpc();
    }

    private void OnEndTurnButtonClicked()
    {
        GameSession.Instance.EndTurnServerRpc();
    }

    public void EnableButtons()
    {
        DrawButton.interactable = true;
        EndTurnButton.interactable = true;
    }

    public void DisableButtons()
    {
        DrawButton.interactable = false;
        EndTurnButton.interactable = false;
    }

    private void OnPlayerJoined(object sender, EventArgs e)
    {
        EnableButtons();
    }
}
