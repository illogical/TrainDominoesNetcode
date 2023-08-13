using Assets.Scripts.Game.States;
using Assets.Scripts.Models;
using System;
using System.Collections.Generic;
using Assets.Scripts.Models.DTO;
using Unity.Netcode;
using UnityEngine;

public class GameSession : NetworkBehaviour
{
    public static GameSession Instance { get; private set; }

    public event EventHandler OnPlayerJoined;
    public event EventHandler<int[]> OnPlayerDrewFromPile;

    [SerializeField] private GameplayManager gameplayManager;

    private GameStateContext gameState;
    private List<ulong> readyPlayers = new List<ulong>();

    // players who have completed their first turn but may not have laid down a track
    private List<ulong> playersCompletedFirstTurn = new List<ulong>(); 

    private bool gameStarted = false;

    private void Awake()
    {
        Instance = this;
        gameplayManager.CreateDominoSet();
        gameplayManager.GetNewEngineDomino();
    }

    private void Start()
    {
        gameStarted = true;
        gameState = new GameStateContext(this, gameplayManager);
    }

    public override void OnNetworkSpawn()
    {
        OnPlayerJoined?.Invoke(this, EventArgs.Empty);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlaceEngineServerRpc(ServerRpcParams serverRpcParams = default)
    {
        DominoEntity engineDomino = gameplayManager.GetEngineDomino();

        PlaceEngineDominoClientRpc(engineDomino.ID, SendToClientSender(serverRpcParams));
    }

    [ClientRpc]
    private void PlaceEngineDominoClientRpc(int dominoId, ClientRpcParams clientRpcParams = default)
    {
        // create a mesh and place it on the side of the screen
        gameplayManager.ClientCreateAndPlaceEngine(dominoId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DrawDominoesServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (gameplayManager.DominoTracker.GetPlayerDominoes(serverRpcParams.Receive.SenderClientId).Count > 0)
        {
            // player already has dominoes so add one to their hand
            var newDominoId = gameplayManager.DrawPlayerDomino(serverRpcParams.Receive.SenderClientId);
            var playerDominoIds =
                gameplayManager.DominoTracker.GetPlayerDominoes(serverRpcParams.Receive.SenderClientId);
            DrawDominoClientRpc(playerDominoIds.ToArray(), newDominoId, SendToClientSender(serverRpcParams));

            // TODO: track that the player drew a domino this turn

            return;
        }

        // this is the first time the player is drawing dominoes

        var dominoIds = gameplayManager.DrawPlayerDominoes(serverRpcParams.Receive.SenderClientId);
        DrawDominoesClientRpc(dominoIds, SendToClientSender(serverRpcParams));
    }

    [ClientRpc]
    private void DrawDominoesClientRpc(int[] dominoIds, ClientRpcParams clientRpcParams)
    {
        OnPlayerDrewFromPile?.Invoke(this, dominoIds);
        gameplayManager.ClientDisplayInitialPlayerDominoes(dominoIds);
    }

    [ClientRpc]
    private void DrawDominoClientRpc(int[] dominoIds, int newDominoId, ClientRpcParams clientRpcParams)
    {
        OnPlayerDrewFromPile?.Invoke(this, dominoIds);
        gameplayManager.ClientDisplayPlayerDominoes(dominoIds, newDominoId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DrawDominoServerRpc(ServerRpcParams serverRpcParams = default)
    {
        int newDominoId = gameplayManager.DrawPlayerDomino(serverRpcParams.Receive.SenderClientId);

        List<int> playerDominoIds =
            gameplayManager.DominoTracker.GetPlayerDominoes(serverRpcParams.Receive.SenderClientId);
        DrawDominoClientRpc(playerDominoIds.ToArray(), newDominoId, serverRpcParams.Receive.SenderClientId,
            SendToClientSender(serverRpcParams));
    }

    [ClientRpc]
    private void DrawDominoClientRpc(int[] playerDominoIds, int newDominoId, ulong clientId,
        ClientRpcParams clientRpcParams)
    {
        var dominoes = new int[] { newDominoId };
        OnPlayerDrewFromPile?.Invoke(this, dominoes);

        Dictionary<int, Transform> playerDominoes = gameplayManager.ClientGetDominoTransforms(playerDominoIds);

        gameplayManager.ClientAddNewDominoForPlayer(playerDominoes, newDominoId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (gameplayManager.TurnManager.IsGroupTurn)
        {
            EndGroupTurnServerRpc(serverRpcParams);
        }
        else
        {
            EndIndividualTurnServerRpc(serverRpcParams);
        }
    }

    [ServerRpc]
    private void EndGroupTurnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // TODO: submit the dominoes that were laid down to the server and add to a new station track
        // TODO: validate that this is not being run a 2nd time for the same player

        ulong senderClientId = serverRpcParams.Receive.SenderClientId;

        if (!playersCompletedFirstTurn.Contains(senderClientId))
        {
            playersCompletedFirstTurn.Add(senderClientId);
        }

        if (readyPlayers.Contains(senderClientId))
        {
            Debug.LogError($"Player {senderClientId} is already ready");
            return;
        }
        else
        {
            Debug.Log($"Player {senderClientId} is ready");
        }

        readyPlayers.Add(senderClientId);

        // TODO: handle submitting multiple dominoes for first track vs adding a single domino

        // first player to draw will set whose turn it is (set to the first player who joined so the host)
        gameplayManager.TurnManager.AddPlayer(senderClientId);

        // TODO: Need to know how many dominoes have been played or which is the player's track or null via a StationManager. If 0 then this player is ending their first turn without laying any dominoes down
        //gameplayManager.SetPlayerLaidFirstTrack(clientId);

        if (readyPlayers.Count == ((MyNetworkManager)NetworkManager.Singleton).Players.Count)
        {
            // this is the last player to end their turn

            readyPlayers.Clear();

            // sync all player stations
            gameplayManager.DominoTracker.MergeTurnTracksIntoStation();
            // get all new dominoes across players
            int[] addedDominoes = gameplayManager.GetUpdatedDominoesForAllPlayers();
            // now sync main station back to all players' TurnStation
            gameplayManager.DominoTracker.SyncMainStationWithPlayerTurnStations();
            
            TurnEndDTO turnEndDto = new TurnEndDTO(gameplayManager.DominoTracker.Station,
                gameplayManager.DominoTracker.GetDominoFlipStatuses(gameplayManager.DominoTracker.Station),
                addedDominoes);
            byte[] turnEndBytes = new NetworkSerializer<TurnEndDTO>().Serialize(turnEndDto);
            // TODO: may want to handle animations for new tracks differently?
            UpdateStationsClientRpc(turnEndBytes);

            gameplayManager.TurnManager.CompleteGroupTurn(); // only the server knows this right now
            
            // TODO: what to do if more than one person can use all dominoes (super edge case)
            
            // this could be any player not just the current one
            ulong? winner = gameplayManager.DominoTracker.CheckPlayerDominoesForWinner();
            if (winner.HasValue)
            {
                // we have a winner!

                EndRoundServerRpc(winner.Value);
                return;
            }

            // TODO: how to wait until animations complete before swapping turns?
            EndGroupTurnClientRpc(gameplayManager.TurnManager.CurrentPlayerId.Value);
        }
    }

    [ServerRpc]
    private void EndIndividualTurnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong senderClientId = serverRpcParams.Receive.SenderClientId;
            
        if (gameplayManager.TurnManager.CurrentPlayerId != senderClientId)
        {
            Debug.LogError($"Player {serverRpcParams.Receive.SenderClientId} is not the current player");
            return;
        }

        // TODO: validate that this player picked up their domino (or automatically deal in the future)
        // TODO: validate that this is not being run a 2nd time for the same player

        // get all new dominoes for the current player
        int[] addedDominoes = gameplayManager.GetUpdatedDominoes(senderClientId);
        // sync current player's station with main station
        gameplayManager.DominoTracker.MergeTurnTrackIntoStation(senderClientId);
        // now sync main station back to all players' TurnStation
        gameplayManager.DominoTracker.SyncMainStationWithPlayerTurnStations();
        // sets the Station to the current player's turn station
        gameplayManager.SubmitPlayerTurnStation(senderClientId);

        // does this player have any remaining dominoes?
        if (gameplayManager.DominoTracker.GetPlayerDominoes(senderClientId).Count == 0)
        {
            // we have a winner!

            EndRoundServerRpc(senderClientId);
            return;
        }
        
        TurnEndDTO turnEndDto = new TurnEndDTO(gameplayManager.DominoTracker.Station,
            gameplayManager.DominoTracker.GetDominoFlipStatuses(gameplayManager.DominoTracker.Station),
            addedDominoes);
        byte[] turnEndBytes = new NetworkSerializer<TurnEndDTO>().Serialize(turnEndDto);
        
        UpdateStationsClientRpc(turnEndBytes);

        EndTurnClientRpc(SendToClientSender(serverRpcParams));

        gameplayManager.TurnManager.IncrementTurn();

        // tell the next player to start their turn
        StartNextPlayerTurnClientRpc(SendToClient(gameplayManager.TurnManager.CurrentPlayerId.Value));
    }

    [ClientRpc]
    private void EndTurnClientRpc(ClientRpcParams clientRpcParams = default)
    {
        gameplayManager.EndPlayerTurn();
    }

    [ClientRpc]
    private void UpdateStationsClientRpc(byte[] turnEndDto)
    {
        // Sync all clients with the main station
        
        // TODO: will need to ignore this on the player whose turn it is. Server may want to specifically send to all other clients

        TurnEndDTO turnEnd = new NetworkSerializer<TurnEndDTO>().Deserialize(turnEndDto);

        // update MeshManager placement based upon the newly added dominoes
        gameplayManager.ClientUpdateStation(turnEnd.MainStation.GetDominoIdsByTracks(), turnEnd.AddedDominoes);
    }

    [ClientRpc]
    private void StartNextPlayerTurnClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("StartNextPlayerTurnClientRpc");
        gameplayManager.StartPlayerTurn(NetworkManager.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SelectDominoServerRpc(int dominoId, ServerRpcParams serverRpcParams = default)
    {
        // TODO: the server needs to decide if the domino can be played. Is it a valid domino to play? Is it a valid track to play on?
        ulong senderClientId = serverRpcParams.Receive.SenderClientId;
        int? selectedDominoId = gameplayManager.DominoTracker.GetSelectedDominoId(senderClientId);

        // TODO: how to break up this logic in the state machine? Trying to stick to client-side logic in the state machine
        
        // extra validation when this isn't the group turn
        if (!gameplayManager.TurnManager.IsGroupTurn)
        {
            if (!gameplayManager.TurnManager.IsPlayerCurrentTurn(senderClientId))
            {
                Debug.LogError($"It is not {senderClientId} player's turn!");
                return;
            }
            
            // player might want to undo their selection by clicking on the domino that the player had added
            TurnStatus turnStatus = gameplayManager.TurnManager.GetPlayerTurnStatus(senderClientId);
            if (turnStatus.PlayedDominoId.HasValue && turnStatus.PlayedDominoId.Value == dominoId)
            {
                // player wants to undo their move and try something else
                UndoMoveServerRpc(dominoId, serverRpcParams);
                return;
            }
                    
            if (gameplayManager.TurnManager.GetPlayerTurnStatus(senderClientId).HasMadeMove)
            {
                return;
            }
        }
        
        // decide if this was a player domino, station domino, or engine domino
        if (gameplayManager.DominoTracker.IsPlayerDomino(senderClientId, dominoId))
        {
            // tell client to select the domino
            SelectPlayerDominoClientRpc(dominoId, gameplayManager.DominoTracker.GetSelectedDominoId(senderClientId) ?? -1,
                SendToClientSender(serverRpcParams));
            gameplayManager.ServerSelectPlayerDomino(senderClientId, dominoId);
        }
        else if (gameplayManager.DominoTracker.IsEngine(dominoId))
        {
            if (!selectedDominoId.HasValue)
            {
                return;
            }
            
            // player wants to add the selected domino to this track

            SelectEngineDominoServerRpc(senderClientId, serverRpcParams);
        }
        else
        {
            // TODO: check train status for this track. DominoManager is likely currently tracking it
            // TODO: account for !gameplayManager.TurnManager.GetPlayerTurnStatus(winnerClientId).HasLaidFirstTrack
            
            Track track = gameplayManager.DominoTracker.GetTurnStationByClientId(senderClientId).GetTrackByDominoId(dominoId);
            if (track.PlayerId != null && track.PlayerId != senderClientId)
            {
                Debug.Log("That track is not yours.");
                return;
            }

            // track domino was clicked
            
            SelectTrackDominoServerRpc(senderClientId, dominoId, serverRpcParams);
        }
    }

    [ServerRpc]
    private void SelectEngineDominoServerRpc(ulong senderClientId, ServerRpcParams serverRpcParams)
    {
        if (gameplayManager.TurnManager.GetPlayerTurnStatus(senderClientId).HasPlayerAddedTrack)
        {
            // a track has already been added this turn
            return;
        }

        if (gameplayManager.DominoTracker.Station.Tracks.Count == 8)
        {
            // a station is full
            Debug.Log("Station is full");
            return;
        }

        int? selectedDominoId = gameplayManager.DominoTracker.GetSelectedDominoId(senderClientId);
        
        // compare the selected domino to the engine domino
        if (!gameplayManager.ServerCompareDominoToEngine(selectedDominoId.Value))
        {
            // selected domino does not match the engine domino 
            return;
        }

        bool flipped = gameplayManager.FlipDominoIfNeeded(selectedDominoId.Value,
            gameplayManager.DominoTracker.GetEngineDominoID());
            
        // TODO: how is it decided that the domino is played on the engine? Is it the first domino played? Is it the highest double? Is it the highest double that is played first?
        var playerTurnStation = gameplayManager.DominoTracker.GetTurnStationByClientId(senderClientId);
        gameplayManager.DominoTracker.PlayDomino(senderClientId, gameplayManager.DominoTracker.GetSelectedDominoId(senderClientId).Value,
            playerTurnStation.Tracks.Count);
        gameplayManager.TurnManager.GetPlayerTurnStatus(senderClientId).PlayerAddedTrack(selectedDominoId.Value);
            
        gameplayManager.DominoTracker.SetSelectedDomino(senderClientId, null);

        JsonContainer stationContainer = new JsonContainer(playerTurnStation);
        SelectEngineDominoClientRpc(selectedDominoId.Value, flipped, stationContainer, SendToClientSender(serverRpcParams));
    }

    [ServerRpc]
    private void SelectTrackDominoServerRpc(ulong senderClientId, int clickedDomino, ServerRpcParams serverRpcParams)
    {
        Station playerTurnStation = gameplayManager.DominoTracker.GetTurnStationByClientId(senderClientId);
        int trackIndex = playerTurnStation.GetTrackIndexByDominoId(clickedDomino).Value;
        var trackDominoId = playerTurnStation.GetTrackByIndex(trackIndex).GetEndDominoId();
        int? selectedDominoId = gameplayManager.DominoTracker.GetSelectedDominoId(senderClientId);

        if (gameplayManager.TurnManager.IsGroupTurn && !selectedDominoId.HasValue)
        {
            // group turn and the player clicked a track domino
            
            // make sure it was the last domino that was clicked
            if (clickedDomino == gameplayManager
                    .DominoTracker
                    .GetTurnStationByClientId(senderClientId)
                    .GetTrackByDominoId(clickedDomino)
                    .GetEndDominoId())
            {
                UndoMoveServerRpc(clickedDomino, serverRpcParams);
            }
            return;
        }

        // compare the domino to the last domino on the track
        if (!gameplayManager.ServerCompareDominoToTrackDomino(selectedDominoId.Value, trackDominoId))
        {
            // selected domino does not match the domino at the end of the selected/clicked track
            return;
        }

        bool flipped = gameplayManager.FlipDominoIfNeeded(selectedDominoId.Value, clickedDomino);
        Debug.Log($"Flipped={flipped}");
        
        gameplayManager.DominoTracker.PlayDomino(senderClientId, selectedDominoId.Value,
            trackIndex);
        gameplayManager.TurnManager.GetPlayerTurnStatus(senderClientId).PlayerMadeMove(selectedDominoId.Value);
        gameplayManager.DominoTracker.SetSelectedDomino(senderClientId, null);

        // get the track index and pass it to the client to move the domino to the track
        JsonContainer stationContainer = new JsonContainer(playerTurnStation);
        SelectTrackDominoClientRpc(selectedDominoId.Value, flipped, trackIndex, stationContainer,
            SendToClientSender(serverRpcParams));
    }


    [ClientRpc]
    private void SelectPlayerDominoClientRpc(int newlySelectedDominoId, int currentlySelectedDominoId,
        ClientRpcParams clientRpcParams = default)
    {
        // cannot serialize null
        int? selectedDominoId = currentlySelectedDominoId < 0 ? null : currentlySelectedDominoId;

        gameplayManager.ClientSelectPlayerDomino(newlySelectedDominoId, selectedDominoId);
    }

    [ClientRpc]
    private void SelectEngineDominoClientRpc(int selectedDominoId, bool isFlipped, JsonContainer tracksWithDominoIds,
        ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Engine domino clicked");

        gameplayManager.ClientAddSelectedToNewTrack(selectedDominoId, isFlipped,
            tracksWithDominoIds.GetDeserializedTrackDominoIds());
    }

    [ClientRpc]
    private void SelectTrackDominoClientRpc(int selectedDominoId, bool isFlipped, int trackIndex, JsonContainer tracksWithDominoIds,
        ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Track domino clicked");

        gameplayManager.ClientAddSelectedDominoToTrack(selectedDominoId, isFlipped, trackIndex,
            tracksWithDominoIds.GetDeserializedTrackDominoIds());
    }
    
    [ServerRpc]
    public void UndoMoveServerRpc(int returnedDominoId, ServerRpcParams serverRpcParams)
    {
        ulong senderClientId = serverRpcParams.Receive.SenderClientId;
        gameplayManager.DominoTracker.ReturnDomino(senderClientId, returnedDominoId);
        gameplayManager.TurnManager.ResetTurn(senderClientId);

        Station playerTurnStation = gameplayManager.DominoTracker.GetTurnStationByClientId(senderClientId);
        List<int> playerDominoes = gameplayManager.DominoTracker.GetPlayerDominoes(senderClientId);
        PlayedDominoes  playedDominoes = new PlayedDominoes(
            playerDominoes.ToArray(), 
            playerTurnStation);
        //JsonContainer playerDominoContainer = new JsonContainer(playerDominoes);

        byte[] playedDominoesDTO = new NetworkSerializer<PlayedDominoes>().Serialize(playedDominoes);
        
        // TODO: need the updated station to pass to the client to trigger the animation

        UndoMoveClientRpc(returnedDominoId, playedDominoesDTO, SendToClientSender(serverRpcParams));
    }

    [ClientRpc]
    private void UndoMoveClientRpc(int requestedDominoId, byte[] playedDominoes, ClientRpcParams clientRpcParams = default)
    {
        PlayedDominoes playedDominoesDTO = new NetworkSerializer<PlayedDominoes>().Deserialize(playedDominoes);
        gameplayManager.ClientRemoveDominoFromTrack(requestedDominoId, playedDominoesDTO.PlayerDominoIds, playedDominoesDTO.Station);
        gameplayManager.PlayerHasReversedMove(requestedDominoId);
    }

    [ClientRpc]
    private void EndGroupTurnClientRpc(ulong clientIdForTurn)
    {
        Debug.Log("Group turn complete");
        gameplayManager.TurnManager.CompleteGroupTurn(); // sync the clients

        if (clientIdForTurn == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("It is now your turn.");

            gameplayManager.StartPlayerTurn(clientIdForTurn);
        }
        else
        {
            Debug.Log($"It is now player {clientIdForTurn}'s turn.");

            gameplayManager.StartAwaitingTurn();
        }

        gameplayManager.CompleteGroupTurn();
    }

    [ServerRpc]
    private void EndRoundServerRpc(ulong winnerClientId)
    {
        var playerScores = gameplayManager.DominoTracker.SumPlayerScores();
        gameplayManager.RoundManager.EndRound(playerScores);
        gameplayManager.TurnManager.SetRoundWinner(winnerClientId);
        
        JsonContainer playerScoresContainer = new JsonContainer(playerScores);
        
        if (gameplayManager.RoundManager.IsLastRound)
        {
            // this was the last round
            EndGameServerRpc(playerScoresContainer);
            return;
        }

        EndRoundClientRpc(winnerClientId, playerScoresContainer);   // send winner and scores to all clients
    }

    [ClientRpc]
    private void EndRoundClientRpc(ulong winnerClientId, JsonContainer playerScores)
    {
        Debug.Log("EndRoundClientRpc");
        gameplayManager.TurnManager.SetRoundWinner(winnerClientId);  // make sure this is set on all players, not just the server.
        gameplayManager.RoundManager.EndRound(playerScores.GetDeserializedPlayerScores()); // all clients need the player scores to display them
        gameplayManager.PlayerWonRound(winnerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    internal void PlayerReadyForNextRoundServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong senderClientId = serverRpcParams.Receive.SenderClientId;
        readyPlayers.Add(senderClientId);
        
        if(readyPlayers.Count == NetworkManager.Singleton.ConnectedClientsIds.Count)
        {
            Debug.Log("All players are ready for the next round.");
            readyPlayers.Clear();

            PrepareForNewRoundServerRpc();
            
            // tell clients to start the next round
            AllPlayersReadyForNextRoundClientRpc();
        }
    }

    [ServerRpc]
    private void PrepareForNewRoundServerRpc()
    {
        gameplayManager.TurnManager.ResetForNextRound();
        gameplayManager.DominoTracker.Reset();
        gameplayManager.GetNewEngineDomino();
        gameplayManager.RoundManager.StartNewRound();
    }

    [ClientRpc]
    private void AllPlayersReadyForNextRoundClientRpc()
    {
        gameplayManager.SetAllPlayersReadyForNextRound();
    }
    
    [ServerRpc]
    private void EndGameServerRpc(JsonContainer playerScores)
    {
        // get the game winner
        // TODO: handle when there is a tie
        var gameWinnerClientId = gameplayManager.RoundManager.GetGameWinners()[0];

        EndGameClientRpc(gameWinnerClientId, playerScores);   // send winner and scores to all clients
    }
    
    [ClientRpc]
    private void EndGameClientRpc(ulong gameWinnerClientId, JsonContainer playerScores)
    {
        // TODO: display GameOverUI (not RoundOverUI)
        Debug.Log("Game over");

        gameplayManager.TurnManager.SetGameWinner(gameWinnerClientId);  // make sure this is set on all players, not just the server.
        gameplayManager.RoundManager.EndRound(playerScores.GetDeserializedPlayerScores()); // all clients need the player scores to display them
        gameplayManager.PlayerWonGame(gameWinnerClientId);
    }
    
    // TODO: private void AllPlayersReadyForNewGameServerRpc()

    private ClientRpcParams SendToClientSender(ServerRpcParams serverRpcParams = default) => new ClientRpcParams
        { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId } } };

    private ClientRpcParams SendToClient(ulong targetClientId) => new ClientRpcParams
        { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetClientId } } };

    [ServerRpc(RequireOwnership = false)]
    internal void PlayerJoinedServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // gameplayManager.TurnManager.AddPlayer(serverRpcParams.Receive.SenderClientId);

        // TODO: handle player removal (this list is also tracked on the DominoPlayer object)
    }

}