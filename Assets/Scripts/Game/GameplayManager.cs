using Assets.Scripts.Game;
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
    [SerializeField] public InputManager InputManager;

    [HideInInspector]
    public DominoTracker DominoTracker;

    public event EventHandler<int> DominoClicked;
    public event EventHandler<int> PlayerDominoSelected;
    public event EventHandler<int> EngineDominoSelected;
    public event EventHandler<int> TrackDominoSelected;

    private TurnManager _turnManager;

    private void Awake()
    {
        _turnManager = new TurnManager();

        DominoTracker = new DominoTracker();
    }

    public void SelectPlayerDomino(int dominoId)
    {
        PlayerDominoSelected?.Invoke(this, dominoId);

        if (!DominoTracker.SelectedDomino.HasValue)
        {
            // raise domino
            layoutManager.SelectDomino(meshManager.GetDominoMeshById(dominoId));
            DominoTracker.SetSelectedDomino(dominoId);
        }
        else if (DominoTracker.SelectedDomino == dominoId)
        {
            // lower domino
            layoutManager.DeselectDomino(meshManager.GetDominoMeshById(dominoId));
            DominoTracker.SetSelectedDomino(null);
        }
        else
        {
            layoutManager.DeselectDomino(meshManager.GetDominoMeshById(DominoTracker.SelectedDomino.Value));
            layoutManager.SelectDomino(meshManager.GetDominoMeshById(dominoId));
            DominoTracker.SetSelectedDomino(dominoId);
        }
    }

    public void CreateDominoSet() => DominoTracker.CreateDominoSet();


    //public void SelectDomino(int dominoId)
    //{
    //    // TODO: decide if this was a player domino, station domino, or engine domino
    //    // TODO: the server needs to decide if the domino can be played
    //    if (DominoTracker.IsPlayerDomino(clientId, dominoId))
    //    {
    //        SelectPlayerDomino(dominoId);
    //    }
    //    else if (DominoTracker.IsEngine(dominoId))
    //    {

    //    }
    //    else
    //    {
    //        // station domino
    //    }
    //}


    public int GetDominoCountPerPlayer(int playerCount)
    {
        // Up to 4 players take 15 dominoes each, 5 or 6 take 12 each, 7 or 8 take 10 each.
        if (playerCount <= 4) { return 15; }
        else if (playerCount <= 6) { return 12; }
        else return 10;
    }

    public int[] DrawPlayerDominoes(ulong clientId)
    {
        return DominoTracker.PickUpDominoes(clientId, 12);
    }

    public int DrawPlayerDomino(ulong clientId)
    {
        return DominoTracker.PickUpDomino(clientId); 
    }

    public DominoEntity GetNewEngineDomino()
    {
        return DominoTracker.GetNextEngineAndCreateStation();
    }

    public DominoEntity GetEngineDomino()
    {
        return DominoTracker.GetEngineDomino();
    }

    public void DisplayPlayerDominoes(int[] dominoIds)
    {
        var playerDominoes = new List<GameObject>();
        foreach (var dominoId in dominoIds)
        {
            playerDominoes.Add(meshManager.CreatePlayerDominoFromInfo(DominoTracker.GetDominoByID(dominoId), new Vector3(0, 1, 0), PurposeType.Player));
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

    public int? GetSelectedDomino() => DominoTracker.SelectedDomino;

    internal void CreateAndPlaceEngine(int dominoId)
    {
        GameObject engineDomino = meshManager.CreateEngineDomino(DominoTracker.GetDominoByID(dominoId), Vector3.zero);
        layoutManager.PlaceEngine(engineDomino);
    }
}
