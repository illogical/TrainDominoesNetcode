using Assets.Scripts.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Models;
using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] private MeshManager meshManager;
    [SerializeField] private LayoutManager layoutManager;
    [SerializeField] public InputManager InputManager;
    [SerializeField] public SoundManager SoundManager;

    [SerializeField] private RoundOverUI roundOverUI;
    [SerializeField] private GameOverUI gameOverUI;
    [SerializeField] private DevMode devMode;

    [HideInInspector] public DominoTracker DominoTracker;
    [HideInInspector] public TurnManager TurnManager;
    [HideInInspector] public RoundManager RoundManager;

    public event EventHandler<int> DominoClicked;
    public event EventHandler<int> PlayerDominoSelected;
    public event EventHandler<int> EngineDominoSelected; 
    public event EventHandler<int> TrackDominoSelected; // pass selected dominoId
    public event EventHandler<ulong> PlayerTurnStarted;
    public event EventHandler<int> PlayerAddedDomino; // pass selected dominoId
    public event EventHandler<int> PlayerAddedTrack; // pass selected dominoId
    public event EventHandler<int> PlayerReversedMove; // pass selected dominoId
    public event EventHandler<ulong> PlayerHasWonRound;
    public event EventHandler<ulong> PlayerHasWonGame;
    public event EventHandler PlayerTurnEnded;
    public event EventHandler GroupTurnEnded;
    public event EventHandler AwaitTurn;
    public event EventHandler AllPlayersReadyForNextRound;

    private void Awake()
    {
        TurnManager = new TurnManager();
        DominoTracker = new DominoTracker();
        DominoTracker.SetEngineIndex(devMode.StartAtRound - 1);
        RoundManager = devMode.StartAtRound == 1 ? new RoundManager() : new RoundManager(devMode.StartAtRound);
    }

    internal void CompleteGroupTurn() => GroupTurnEnded?.Invoke(this, EventArgs.Empty);
    internal void StartPlayerTurn(ulong clientId) => PlayerTurnStarted?.Invoke(this, clientId);
    internal void EndPlayerTurn() => PlayerTurnEnded?.Invoke(this, EventArgs.Empty);
    internal void StartAwaitingTurn() => AwaitTurn?.Invoke(this, EventArgs.Empty);
    internal void SetAllPlayersReadyForNextRound() => AllPlayersReadyForNextRound?.Invoke(this, EventArgs.Empty);
    internal void PlayerHasReversedMove(int returnedDominoId) => PlayerReversedMove?.Invoke(this, returnedDominoId);
    internal void PlayerWonRound(ulong winnerClientId) => PlayerHasWonRound?.Invoke(this, winnerClientId);
    internal void PlayerWonGame(ulong winnerClientId) => PlayerHasWonGame?.Invoke(this, winnerClientId);

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
        if (playerCount <= 4)
        {
            return 15;
        }
        else if (playerCount <= 6)
        {
            return 12;
        }
        else return 10;
    }

    public Dictionary<int, Transform> ClientGetDominoTransforms(int[] dominoIds)
    {
        var dominoTransforms = new Dictionary<int, Transform>();

        foreach (int dominoId in dominoIds)
        {
            GameObject dominoMesh = meshManager.GetDominoMeshById(dominoId);
            if (dominoMesh == null)
            {
                // create the mesh
                dominoMesh = meshManager.CreatePlayerDominoFromInfo(DominoTracker.GetDominoByID(dominoId),
                    new Vector3(0, 1, 0), PurposeType.Track);
            }

            dominoTransforms.Add(dominoId, dominoMesh.transform);
        }

        return dominoTransforms;
    }

    public int[] DrawPlayerDominoes(ulong clientId)
    {
        // TODO: take GetDominoCountPerPlayer(playerCount) into account
        //      GetDominoCountPerPlayer(int playerCount);
        return DominoTracker.PickUpDominoes(clientId, devMode.DominoStartCount);
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

    public bool FlipDominoIfNeeded(int dominoId, int destinationDominoId)
    {
        DominoEntity selectedDominoInfo = DominoTracker.GetDominoByID(dominoId);
        DominoEntity engineDominoInfo = DominoTracker.GetDominoByID(destinationDominoId);

        bool isFlipped = DominoTracker.IsDominoFlipNeeded(selectedDominoInfo, engineDominoInfo);
        selectedDominoInfo.Flipped = isFlipped;
        return isFlipped;
    }

    internal void ClientFlipDominoMesh(int dominoId, bool isFlipped)
    {
        DominoEntity selectedDominoInfo = DominoTracker.GetDominoByID(dominoId);
        selectedDominoInfo.Flipped = isFlipped;
        meshManager.UpdateDomino(selectedDominoInfo);
    }

    public void ClientDisplayInitialPlayerDominoes(int[] dominoIds)
    {
        var playerDominoes = new List<GameObject>();
        foreach (var dominoId in dominoIds)
        {
            // use the mesh if it already exists, otherwise create it
            var dominoMesh = meshManager.GetDominoMeshById(dominoId);
            if (dominoMesh == null)
            {
                dominoMesh = meshManager.CreatePlayerDominoFromInfo(DominoTracker.GetDominoByID(dominoId),
                    new Vector3(0, 1, 0), PurposeType.Player);
            }

            playerDominoes.Add(dominoMesh);
        }

        layoutManager.PlaceInitialPlayerDominoes(playerDominoes);
    }

    public void ClientDisplayPlayerDominoes(int[] dominoIds, int newDominoId)
    {
        var meshes = meshManager.GetDominoMeshesByEntities(DominoTracker.GetDominoesByIDs(dominoIds));

        layoutManager.AddNewDominoForPlayer(meshes, newDominoId);
    }

    internal void ClientCreateAndPlaceEngine(int dominoId)
    {
        GameObject engineDomino = meshManager.CreateEngineDomino(DominoTracker.GetDominoByID(dominoId), Vector3.zero);
        layoutManager.PlaceEngine(engineDomino);
    }

    public void ServerSelectPlayerDomino(ulong clientId, int dominoId)
    {
        if (!DominoTracker.GetSelectedDominoId(clientId).HasValue)
        {
            // first time a domino is being selected
            DominoTracker.SetSelectedDomino(clientId, dominoId);
        }
        else if (DominoTracker.GetSelectedDominoId(clientId) == dominoId)
        {
            // same domino was clicked again. Deselect it.
            DominoTracker.SetSelectedDomino(clientId, null);
        }
        else
        {
            // new domino is being selected
            DominoTracker.SetSelectedDomino(clientId, dominoId);
        }
    }

    public void ClientAddSelectedToNewTrack(int selectedDominoId, bool isFlipped, Station station, bool isGroupTurn)
    {
        float trackSlideDuration = 0.3f;
        GameObject currentObj = meshManager.GetDominoMeshById(selectedDominoId);

        PlayerAddedDomino?.Invoke(this, selectedDominoId);
        PlayerAddedTrack?.Invoke(this, selectedDominoId);

        ClientFlipDominoMesh(selectedDominoId, isFlipped);

        // move empties to move the lines and animate the selected box moving to the track
        StartCoroutine(layoutManager.AddDominoToNewTrackAndUpdateTrackPositions(currentObj.transform, selectedDominoId,
            station.GetDominoIdsByTracks(), meshManager,
            trackSlideDuration));
        
        if (!isGroupTurn)
        {
            // technically updates the Y position for the track message(s)
            ClientUpdateTrackLabels(station);
        }
    }

    public void ClientRearrangePlayerDominoes(int[] playerDominoIds)
    {
        var dominoTransforms = meshManager.GetDominoTransformsByIds(playerDominoIds);
        StartCoroutine(layoutManager.UpdatePlayerPositions(dominoTransforms));
    }

    public void ClientAddSelectedDominoToTrack(int selectedDominoId, bool isFlipped, int trackIndex,
        Station station, bool isGroupTurn)
    {
        float trackSlideDuration = 0.3f;
        GameObject currentObj = meshManager.GetDominoMeshById(selectedDominoId);
        // TODO: flip domino here
        ClientFlipDominoMesh(selectedDominoId, isFlipped);
        
        PlayerAddedDomino?.Invoke(this, selectedDominoId);
        
        StartCoroutine(layoutManager.AddDominoToExistingTrack(currentObj.transform, station.GetDominoIdsByTracks(),
            trackIndex));
        
        if (!isGroupTurn)
        {
            // technically updates the X position for the track message(s)
            ClientUpdateTrackLabels(station);
        }
    }
    
    public void ClientRemoveDominoFromTrack(int returnedDominoId, int[] playerDominoes,
        Station station)
    {
        var playerDominoIds = DominoTracker.GetDominoesByIDs(playerDominoes);
        var playerDominoMeshes = meshManager.GetDominoMeshesByEntities(playerDominoIds);
        
        // update track positions and return the domino to the player
        List<List<int>> trackDominoIds = station.GetDominoIdsByTracks();
        var trackDominoMeshes = meshManager.GetDominoTransformsByIds(station.GetAllStationDominoIds().ToArray());
        layoutManager.ReturnDominoToPlayer(playerDominoMeshes, returnedDominoId);
        layoutManager.UpdateStationPositions(trackDominoIds, trackDominoMeshes);
        
        // update track label positions
        ClientUpdateTrackLabels(station);
    }

    public bool ServerCompareDominoToEngine(int dominoId)
    {
        if (devMode.IgnoreDominoComparisons)
        {
            return true;
        }
        
        var engineDomino = DominoTracker.GetEngineDomino();
        return DominoTracker.CompareDominoes(dominoId, engineDomino.ID);
    }
    
    public bool ServerCompareDominoToTrackDomino(int dominoId, int trackDominoId)
    {
        if (devMode.IgnoreDominoComparisons)
        {
            return true;
        }
        
        var trackDomino = DominoTracker.GetDominoByID(trackDominoId);
        return DominoTracker.CompareDominoes(dominoId, trackDomino.ID);
    }

    internal void ClientAddNewDominoForPlayer(Dictionary<int, Transform> playerDominoes, int dominoId)
    {
        layoutManager.AddNewDominoForPlayer(playerDominoes, dominoId);
    }

    internal void ClientUpdateStation(Station station, int[] addDominoIds)
    {
        // TODO: additional animation for new dominoes
        // TODO: start from position above top of screen or right side

        List<int> allTrackDominoIds = new List<int>();
        foreach (var track in station.Tracks)
        {
            allTrackDominoIds.AddRange(track.DominoIds);
        }

        layoutManager.UpdateStationPositions(station.GetDominoIdsByTracks(), 
            ClientGetDominoTransforms(allTrackDominoIds.ToArray()), 
            () => ClientUpdateTrackLabels(station));
        
    }

    private void ClientUpdateTrackLabels(Station station)
    {
        // TODO: hide all track labels that are not included in this station? (player just removed a track)
        meshManager.DisableAllTrackLabels();
        
        Dictionary<int, GameObject> trackLabelObjectsByTrackIndex = new Dictionary<int, GameObject>();
        Dictionary<int, Vector3> tracLabelDestinationPositionsByTrackIndex = new Dictionary<int, Vector3>();
        
        for (int i = 0; i < station.Tracks.Count; i++)
        {
            if (!station.Tracks[i].PlayerId.HasValue)
            {
                continue;
            }
            
            Vector3 lastDominoPosition = layoutManager.GetTrackPosition(station.Tracks[i].DominoIds.Count - 1, i, station.Tracks.Count);
            Vector3 destinationPosition = layoutManager.GetTrackLabelPosition(lastDominoPosition);
            GameObject trackMessageObject = meshManager.GetTrackLabelByTrackIndex(i)
                                            ?? meshManager.SetTrackLabelForTrack(i, destinationPosition,
                                                $"Player {station.Tracks[i].PlayerId.ToString()}");

            trackMessageObject.gameObject.SetActive(true);
            trackLabelObjectsByTrackIndex.Add(i, trackMessageObject);
            tracLabelDestinationPositionsByTrackIndex.Add(i, destinationPosition);
        }
        layoutManager.UpdateTrackLabelPositions(station, trackLabelObjectsByTrackIndex, tracLabelDestinationPositionsByTrackIndex);
    }
    
    public void ClientUpdateFlipStatuses(Dictionary<int, bool> turnEndDominoFlipInfo)
    {
        foreach (var dominoId in turnEndDominoFlipInfo.Keys)
        {
            DominoEntity dominoInfo = DominoTracker.GetDominoByID(dominoId);
            dominoInfo.Flipped = turnEndDominoFlipInfo[dominoId];
        }
    }

    public void RoundIsOver(ulong winnerClientId, Dictionary<ulong, int> playerScores,
        Dictionary<ulong, int> playerTotals)
    {
        Debug.Log($"{playerScores.Count} playerScores provided for a total of {playerScores.Sum(p => p.Value)}");
        // the player who has 0 is the winner but we also know who just ended their turn and played their last domino
        roundOverUI.Show(winnerClientId.ToString(), playerScores, playerTotals);
    }

    public void ClientResetForNextRound()
    {
        roundOverUI.Hide();
        meshManager.ResetDominoMeshes();
        InputManager.SetRoundReadyButtonEnabled(true);
    }

    public void GameIsOver(ulong winnerClientId, Dictionary<ulong, int> playerScores,
        Dictionary<ulong, int> playerTotals)
    {
        Debug.Log($"{playerScores.Count} playerScores provided for a total of {playerScores.Sum(p => p.Value)}");
        gameOverUI.Show(winnerClientId.ToString(), playerScores, playerTotals);
    }

    public int[] GetUpdatedDominoesForAllPlayers() => DominoTracker.GetDominoesFromTurnStations();

    public int[] GetUpdatedDominoes(ulong clientId) => DominoTracker.Station.GetNewDominoesByComparingToStation(
        DominoTracker.GetTurnStationByClientId(clientId));

    public void SubmitPlayerTurnStation(ulong clientId) => DominoTracker.UpdateStationToPlayerTurnStation(clientId);
}