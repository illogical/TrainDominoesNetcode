using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI winnerNameText;
    [SerializeField] private PlayerScoreTemplate playerScoreTemplate;

    private Dictionary<ulong, int> _playerScores;

    // TODO: dynamically create list of players + scores

    private void Start()
    {
        Hide();
    }

    public void Show(string winnerName, Dictionary<ulong, int> playerScores)
    {
        winnerNameText.text = winnerName;
        _playerScores = playerScores;
        
        gameObject.SetActive(true);
    }
    public void Hide() => gameObject.SetActive(false);
}
