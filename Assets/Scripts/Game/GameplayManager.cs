using Assets.Scripts.Game;
using Assets.Scripts.Helpers;
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
    public event EventHandler<ulong> PlayerTurnStarted;
    public event EventHandler PlayerTurnEnded;
    public event EventHandler GroupTurnEnded;
    public event EventHandler AwaitTurn;



    private void Awake()
    {
        TurnManager = new TurnManager();
        DominoTracker = new DominoTracker();
    }

    public void ClientSelectPlayerDomino(int newSelectedDominoId, int? currentlySelectedDominoId)
    {
        PlayerDominoSelected?.Invoke(this, newSelectedDominoId);

        if (!currentlySelectedDominoId.HasValue)
        {
            // raise domino
            layoutManager.SelectDomino(meshManager.GetDominoMeshById(newSelectedDominoId));
        }
        else if (currentlySelectedDominoId == newSelectedDominoId)
        {
            // lower domino
            layoutManager.DeselectDomino(meshManager.GetDominoMeshById(newSelectedDominoId));
        }
        else
        {
            layoutManager.DeselectDomino(meshManager.GetDominoMeshById(currentlySelectedDominoId.Value));
            layoutManager.SelectDomino(meshManager.GetDominoMeshById(newSelectedDominoId));
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

    public void ClientDisplayPlayerDominoes(int[] dominoIds)
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

    internal void ClientCreateAndPlaceEngine(int dominoId)
    {
        GameObject engineDomino = meshManager.CreateEngineDomino(DominoTracker.GetDominoByID(dominoId), Vector3.zero);
        layoutManager.PlaceEngine(engineDomino);
    }

    internal void CompleteGroupTurn() => GroupTurnEnded?.Invoke(this, EventArgs.Empty);
    internal void StartPlayerTurn(ulong clientId) => PlayerTurnStarted?.Invoke(this, clientId);
    internal void EndPlayerTurn() => PlayerTurnEnded?.Invoke(this, EventArgs.Empty);
    internal void StartAwaitingTurn() => AwaitTurn?.Invoke(this, EventArgs.Empty);

    //internal void AddDominoToNewTrack(int dominoId)
    //{
    //    if (!CompareDominoes(selectedDominoId, DominoTracker.GetEngineDominoID()))
    //    {
    //        Debug.Log("Those don't match, nice try.");
    //        return;
    //    }

    //    // add domino to track
    //    var newTrack = DominoTracker.Station.AddTrack(selectedDominoId);

    //    var startPosition = PositionHelper.GetScreenLeftCenter(mainCamera);
    //}

    public void ServerSelectPlayerDomino(int dominoId)
    {
        if (!DominoTracker.SelectedDomino.HasValue)
        {
            DominoTracker.SetSelectedDomino(dominoId);
        }
        else if (DominoTracker.SelectedDomino == dominoId)
        {
            DominoTracker.SetSelectedDomino(null);
        }
        else
        {
            DominoTracker.SetSelectedDomino(dominoId);
        }
    }

    public void ClientAddSelectedToNewTrack(int selectedDominoId, List<List<int>> tracksWithDomininoIds)
    {
        float trackSlideDuration = 0.3f;
        GameObject currentObj = meshManager.GetDominoMeshById(selectedDominoId);

        Debug.Log(tracksWithDomininoIds.Count + " tracks");
        Debug.Log(tracksWithDomininoIds[0].Count + " dominos on first track");

        // move empties to move the lines and animate the selected box moving to the track
        StartCoroutine(layoutManager.AddNewDominoAndUpdateTrackPositions(currentObj.transform, selectedDominoId, tracksWithDomininoIds, meshManager, trackSlideDuration));
    }

    public void ClientAddSelectedDominoToTrack(int selectedDominoId, int trackIndex, List<List<int>> tracksWithDominoIds)
    {
        float trackSlideDuration = 0.3f;
        GameObject currentObj = meshManager.GetDominoMeshById(selectedDominoId);

        // move empties to move the lines and animate the selected box moving to the track
        StartCoroutine(layoutManager.AddDominoAndUpdateTrackPositions(currentObj.transform, tracksWithDominoIds, meshManager, trackIndex, trackSlideDuration));
    }

    public bool ServerCompareDominoToEngine(int dominoId)
    {
        var engineDomino = DominoTracker.GetEngineDomino();
        return CompareDominoes(dominoId, engineDomino.ID);
    }

    private bool CompareDominoes(int playerSelectedDominoId, int otherDominoId)
    {
        var trackDomino = DominoTracker.GetDominoByID(otherDominoId);
        var selectedDomino = DominoTracker.GetDominoByID(playerSelectedDominoId);

        // take into account flipped track dominoes
        var trackScoreToCompare = trackDomino.Flipped ? trackDomino.BottomScore : trackDomino.TopScore;

        // TODO: fix this after the domino knows if it wants to be flipped
        //return trackScoreToCompare == selectedDomino.BottomScore
        //|| trackScoreToCompare == selectedDomino.TopScore;
        return trackDomino.TopScore == selectedDomino.BottomScore
        || trackDomino.BottomScore == selectedDomino.TopScore
        || trackDomino.TopScore == selectedDomino.TopScore
        || trackDomino.BottomScore == selectedDomino.BottomScore;
    }

    internal void AddDominoToExistingTrack(int dominoId)
    {
        throw new NotImplementedException();
    }
}
