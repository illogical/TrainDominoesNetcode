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
        DisableButtons();
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
}
