using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI winnerNameText;
    [SerializeField] private PlayerScoreTemplate playerScoreTemplate;
    [SerializeField] private Transform playerScoreTemplateParent;

    private Dictionary<ulong, int> _playerScores;
    private Dictionary<ulong, PlayerScoreTemplate> _playerScoreTemplates = new Dictionary<ulong, PlayerScoreTemplate>();    

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
        foreach (var playerId in playerScores.Keys)
        {
            PlayerScoreTemplate scoreTemplate;
            if (!_playerScoreTemplates.ContainsKey(playerId))
            {
                scoreTemplate = Instantiate(playerScoreTemplate, playerScoreTemplateParent);
                _playerScoreTemplates.Add(playerId, scoreTemplate);
                scoreTemplate.gameObject.SetActive(true);
            }
            else
            {
                scoreTemplate = _playerScoreTemplates[playerId];
            }

            scoreTemplate.PlayerNameText.text = playerId.ToString();
            scoreTemplate.PlayerScoreText.text = playerScores[playerId].ToString();
            scoreTemplate.PlayerTotalText.text = playerTotals[playerId].ToString();
        }
    }
}
