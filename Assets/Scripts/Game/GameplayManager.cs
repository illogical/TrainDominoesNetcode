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
    [HideInInspector]
    public TurnManager TurnManager;

    public event EventHandler<int> DominoClicked;
    public event EventHandler<int> PlayerDominoSelected;
    public event EventHandler<int> EngineDominoSelected;
    public event EventHandler<int> TrackDominoSelected;
    public event EventHandler TurnCompleted;
    public event EventHandler PlayerTurnStarted;
    public event EventHandler AwaitTurn;



    private void Awake()
    {
        TurnManager = new TurnManager();
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

    public bool HasPlayerLaidFirstTrack(ulong clientId)
    {
        return TurnManager.HasPlayerLaidFirstTrack(clientId);
    }

    public void SetPlayerLaidFirstTrack(ulong clientId)
    {
        TurnManager.CompleteLaidFirstTrack(clientId);
    }

    public int? GetSelectedDomino() => DominoTracker.SelectedDomino;

    internal void CreateAndPlaceEngine(int dominoId)
    {
        GameObject engineDomino = meshManager.CreateEngineDomino(DominoTracker.GetDominoByID(dominoId), Vector3.zero);
        layoutManager.PlaceEngine(engineDomino);
    }

    internal void CompleteTurn() => TurnCompleted?.Invoke(this, EventArgs.Empty);
    internal void StartPlayerTurn() => PlayerTurnStarted?.Invoke(this, EventArgs.Empty);
    internal void StartAwaitingTurn() => AwaitTurn?.Invoke(this, EventArgs.Empty);

}
