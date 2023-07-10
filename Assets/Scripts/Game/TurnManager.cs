using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager
{
    private ulong? _currentPlayerId;
    private int _currentTurn;

    private List<ulong> _laidFirstTrack;

    public TurnManager()
    {
        _currentTurn = 0;
        _laidFirstTrack = new List<ulong>();
    }
    public bool HaveAllPlayersLaidTrack(int playerCount) => _laidFirstTrack.Count == playerCount;
    public bool HasPlayerLaidFirstTrack(ulong playerId) => _laidFirstTrack.Contains(playerId);

    public void CompleteLaidFirstTrack(ulong playerId)
    {
        _laidFirstTrack.Add(playerId);
    }


    public void SetCurrentPlayerId(ulong playerId)
    {
        _currentPlayerId = playerId;
    }   

    public ulong? CurrentPlayerId => _currentPlayerId;
    public int CurrentTurn => _currentTurn;
}
