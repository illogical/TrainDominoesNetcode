using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager
{
    private ulong? _currentPlayerId;
    private int _currentTurn;

    private List<ulong> _allPlayers = new List<ulong>();
    private List<ulong> _laidFirstTrack;

    public TurnManager()
    {
        _currentTurn = 0;
        _laidFirstTrack = new List<ulong>();
    }

    public void AddPlayer(ulong clientId)
    {
        if(!_allPlayers.Contains(clientId))
        {
            _allPlayers.Add(clientId);
        }
    }
    
    public bool HaveAllPlayersLaidTrack(int playerCount) => _laidFirstTrack.Count == playerCount;
    public bool HasPlayerLaidFirstTrack(ulong playerId) => _laidFirstTrack.Contains(playerId);

    public void CompleteLaidFirstTrack(ulong playerId)
    {
        if(!_laidFirstTrack.Contains(playerId))
        {
            _laidFirstTrack.Add(playerId);
        }
    }

    public void IncrementTurn()
    {
        if(_allPlayers.Count < 1)
        {
            Debug.LogError("Error: TurnManager needs players to track turns");
            return;
        }

        _currentTurn++;
    }

    public ulong? CurrentPlayerId => _allPlayers[CurrentTurn];
    public int CurrentTurn => _currentTurn % _allPlayers.Count;
}
