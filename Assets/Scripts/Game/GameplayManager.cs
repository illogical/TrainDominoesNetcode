using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    // TODO: Handle all server operations here

    [SerializeField] private MeshManager meshManager;
    [SerializeField] private LayoutManager layoutManager;

    private DominoManager _dominoManager;
    private TurnManager _turnManager;
    private StationManager _stationManager;

    private void Awake()
    {
        _dominoManager = new DominoManager();
        _turnManager = new TurnManager();
        _stationManager = new StationManager();
    }

    public void CreateDominoSet() => _dominoManager.CreateDominoSet();

    public int GetDominoCountPerPlayer(int playerCount)
    {
        // Up to 4 players take 15 dominoes each, 5 or 6 take 12 each, 7 or 8 take 10 each.
        if (playerCount <= 4) { return 15; }
        else if (playerCount <= 6) { return 12; }
        else return 10;
    }

    public int[] DrawPlayerDominoes(ulong clientId)
    {
        _dominoManager.PickUpDominoes(clientId, 12);
        var myDominoes = _dominoManager.GetPlayerDominoes(clientId);
        return myDominoes.Select(d => d.ID).ToArray();
    }

    public void DisplayPlayerDominoes(int[] dominoIds)
    {
        var playerDominoes = new List<GameObject>();
        foreach (var dominoId in dominoIds)
        {
            playerDominoes.Add(meshManager.CreatePlayerDominoFromInfo(_dominoManager.GetDominoByID(dominoId), new Vector3(0, 1, 0), PurposeType.Player));
        }

        layoutManager.PlacePlayerDominoes(playerDominoes);
    }

    public ulong? GetPlayerIdForTurn()
    {
        return _turnManager.CurrentPlayerId;
    }

    public void HasPlayerLaidFirstTrack(ulong clientId)
    {
        _turnManager.HasPlayerLaidFirstTrack(clientId);
    }

    public void SetPlayerLaidFirstTrack(ulong clientId)
    {
        _turnManager.CompleteLaidFirstTrack(clientId);
    }

    public void SetPlayerTurn(ulong clientId)
    {
        _turnManager.SetCurrentPlayerId(clientId);
        Debug.Log($"{clientId} player's turn set");
    }


}
