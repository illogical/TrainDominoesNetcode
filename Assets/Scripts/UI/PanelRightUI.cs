using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelRightUI : MonoBehaviour
{

    [SerializeField] private Button drawButton;
    [SerializeField] private Button endTurnButton;

    private void Start()
    {
        drawButton.onClick.AddListener(OnDrawButtonClicked);
        endTurnButton.onClick.AddListener(OnEndTurnButtonClicked);
    }

    private void OnDrawButtonClicked()
    {
        GameSession.Instance.DrawDominoesServerRpc();
    }

    private void OnEndTurnButtonClicked()
    {
        GameSession.Instance.EndTurnServerRpc();
    }
}
