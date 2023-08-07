using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Models;
using UnityEngine;

public class TurnManager
{
    private int _currentTurn;

    private List<ulong> _allPlayers = new List<ulong>();  // these are in the order that they ended the group turn
    private Dictionary<ulong, TurnStatus> _playerTurnStatuses;
    private bool _isGroupTurn = true;
    private ulong? _roundWinnerClientId;
    private ulong? _gameWinnerClientId;

    public TurnManager()
    {
        _currentTurn = 0;
        _playerTurnStatuses = new Dictionary<ulong, TurnStatus>();

        ResetPlayerTurnStates();
    }

    public void AddPlayer(ulong clientId)
    {
        if(!_allPlayers.Contains(clientId))
        {
            _allPlayers.Add(clientId);
        }

        if (!_playerTurnStatuses.ContainsKey(clientId))
        {
            _playerTurnStatuses.Add(clientId, new TurnStatus());
        }
    }
    
    private void ResetPlayerTurnStates()
    {
        foreach (ulong clientId in _playerTurnStatuses.Keys)
        {
            GetPlayerTurnStatus(clientId).ResetTurnStatus();
        }
    }
    
    public void CompleteGroupTurn()
    {
        _isGroupTurn = false;

        ResetPlayerTurnStates();
    }

    public void IncrementTurn()
    {
        if(_allPlayers.Count < 1)
        {
            Debug.LogError("Error: TurnManager needs players to track turns");
            return;
        }

        // reset all player's turn-specific state
        ResetPlayerTurnStates();
        
        _currentTurn++;
    }

    public void ResetForNextRound()
    {
        _currentTurn = 0;
        _roundWinnerClientId = null;
        _isGroupTurn = true;
        _playerTurnStatuses.Clear();
    }
    
    public void ResetTurn(ulong clientId)
    {
        _playerTurnStatuses[clientId].ResetTurnStatus();
    }
    
    public TurnStatus GetPlayerTurnStatus(ulong clientId)
    {
        if (!_playerTurnStatuses.ContainsKey(clientId))
        {
            _playerTurnStatuses.Add(clientId, new TurnStatus());
        }
        
        return _playerTurnStatuses[clientId];
    }

    public void SetRoundWinner(ulong clientId) => _roundWinnerClientId = clientId;
    public ulong? GetRoundWinnerClientId() => _roundWinnerClientId;
    
    public void SetGameWinner(ulong clientId) => _gameWinnerClientId = clientId;
    public ulong? GetGameWinnerClientId() => _gameWinnerClientId;
    public ulong? CurrentPlayerId => _allPlayers[CurrentTurn];
    public int CurrentTurn => _currentTurn % _allPlayers.Count;
    public bool IsPlayerCurrentTurn(ulong clientId) => CurrentPlayerId == clientId;
    public bool IsGroupTurn => _isGroupTurn;
}
