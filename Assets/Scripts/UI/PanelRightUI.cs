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
        GameSession.Instance.OnPlayerDrewFromPile += OnPlayerDrewFromPile;

        DisableButtons();
    }

    private void OnDrawButtonClicked()
    {
        // TODO: execute event instead of calling server rpc
        
        GameSession.Instance.DrawInitialDominoesServerRpc();
    }

    private void OnEndTurnButtonClicked()
    {
        // TODO: execute event instead of calling server rpc

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

    private void OnPlayerDrewFromPile(object sender, int[] e)
    {
        DrawButton.interactable = false;
    }
}
