using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI winnerNameText;
    [SerializeField] private PlayerScoreTemplate playerScoreTemplate;
    [SerializeField] private Transform playerScoreTemplateParent;

    private Dictionary<ulong, int> _playerScores;

    // TODO: dynamically create list of players + scores

    private void Start()
    {
        Hide();
    }

    public void Show(string winnerName, Dictionary<ulong, int> playerScores, Dictionary<ulong, int> playerTotals)
    {
        winnerNameText.text = winnerName;
        _playerScores = playerScores;

        DisplayScores(playerScores, playerTotals);
        
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        playerScoreTemplate.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void DisplayScores(Dictionary<ulong, int> playerScores, Dictionary<ulong, int> playerTotals)
    {
        // TODO: how to clear these to prepare for the next round? Might need to track which player each belongs to (to update later)
        foreach (var playerId in playerScores.Keys)
        {
            PlayerScoreTemplate scoreTemplate = Instantiate(playerScoreTemplate, playerScoreTemplateParent);
            scoreTemplate.gameObject.SetActive(true);

            scoreTemplate.PlayerNameText.text = playerId.ToString();
            scoreTemplate.PlayerScoreText.text = playerScores[playerId].ToString();
            scoreTemplate.PlayerTotalText.text = playerTotals[playerId].ToString();
        }
    }
}
