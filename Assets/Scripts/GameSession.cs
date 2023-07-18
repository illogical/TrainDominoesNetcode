using Assets.Scripts.Game.States;
using Assets.Scripts.Models;
using System;
using System.Collections.Generic;
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
            EndPlayerTurnServerRpc(serverRpcParams);
        }
    }

    [ServerRpc]
    private void EndGroupTurnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // TODO: submit the dominoes that were laid down to the server and add to a new station track
        // TODO: validate that this is not being run a 2nd time for the same player

        ulong clientId = serverRpcParams.Receive.SenderClientId;

        if (!playersCompletedFirstTurn.Contains(clientId))
        {
            playersCompletedFirstTurn.Add(clientId);
        }

        if (readyPlayers.Contains(clientId))
        {
            Debug.LogError($"Player {clientId} is already ready");
            return;
        }
        else
        {
            Debug.Log($"Player {clientId} is ready");
        }

        readyPlayers.Add(clientId);

        // TODO: handle submitting multiple dominoes for first track vs adding a single domino

        // first player to draw will set whose turn it is (set to the first player who joined so the host)
        gameplayManager.TurnManager.AddPlayer(clientId);

        // TODO: Need to know how many dominoes have been played or which is the player's track or null via a StationManager. If 0 then this player is ending their first turn without laying any dominoes down
        //gameplayManager.SetPlayerLaidFirstTrack(clientId);

        if (readyPlayers.Count == ((MyNetworkManager)NetworkManager.Singleton).Players.Count)
        {
            // this is the last player to end their turn

            // sync all player stations
            gameplayManager.DominoTracker.MergeTurnTracksIntoStation();
            // get all new dominoes across players
            int[] addedDominoes = gameplayManager.GetUpdatedDominoesForAllPlayers();
            // now sync main station back to all players' TurnStation
            gameplayManager.DominoTracker.SyncMainStationWithPlayerTurnStations();


            JsonContainer stationContainer = new JsonContainer(gameplayManager.DominoTracker.Station);
            // TODO: may want to handle animations for new tracks differently?
            UpdateStationsClientRpc(stationContainer, addedDominoes);

            gameplayManager.TurnManager.CompleteGroupTurn();

            // TODO: how to wait until animations complete before swapping turns?
            PlayerReadyClientRpc(gameplayManager.TurnManager.CurrentPlayerId.Value);
        }
    }

    [ServerRpc]
    private void EndPlayerTurnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (gameplayManager.TurnManager.CurrentPlayerId != serverRpcParams.Receive.SenderClientId)
        {
            Debug.LogError($"Player {serverRpcParams.Receive.SenderClientId} is not the current player");
            return;
        }

        // TODO: validate that this player picked up their domino (or automatically deal in the future)
        // TODO: validate that this is not being run a 2nd time for the same player

        // get all new dominoes for the current player
        int[] addedDominoes = gameplayManager.GetUpdatedDominoes(serverRpcParams.Receive.SenderClientId);
        // sync current player's station with main station
        gameplayManager.DominoTracker.MergeTurnTrackIntoStation(serverRpcParams.Receive.SenderClientId);
        // now sync main station back to all players' TurnStation
        gameplayManager.DominoTracker.SyncMainStationWithPlayerTurnStations();

        // sets the Station to the current player's turn station
        gameplayManager.SubmitPlayerTurnStation(serverRpcParams.Receive.SenderClientId);

        JsonContainer stationContainer = new JsonContainer(gameplayManager.DominoTracker.Station);
        UpdateStationsClientRpc(stationContainer, addedDominoes);

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
    private void UpdateStationsClientRpc(JsonContainer tracksWithDominoIds, int[] addDominoIds)
    {
        Debug.Log($"Recieved {addDominoIds.Length} new dominoes");

        // TODO: will need to ignore this on the player whose turn it is. Server may want to specifically send to all other clients

        // update MeshManager placement based upon the newly added dominoes
        gameplayManager.ClientUpdateStation(tracksWithDominoIds.GetDeserializedTrackDominoIds(), addDominoIds);
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

        // TODO: how to break up this logic in the state machine?
        // extra validation when this isn't the group turn
        if (!gameplayManager.TurnManager.IsGroupTurn)
        {
            if (!gameplayManager.TurnManager.IsPlayerCurrentTurn(senderClientId))
            {
                Debug.LogError($"It is not {senderClientId} player's turn!");
                return;
            }

            if (gameplayManager.TurnManager.GetPlayerTurnStatus(senderClientId).HasMadeMove)
            {
                Debug.LogError($"Player has already made a move. Don't cheat!");
                return;
            }
        }
        
 
        // decide if this was a player domino, station domino, or engine domino
        if (gameplayManager.DominoTracker.IsPlayerDomino(senderClientId, dominoId))
        {
            // tell client to select the domino
            SelectPlayerDominoClientRpc(dominoId, gameplayManager.DominoTracker.SelectedDomino ?? -1,
                SendToClientSender(serverRpcParams));
            gameplayManager.ServerSelectPlayerDomino(dominoId);
        }
        else if (gameplayManager.DominoTracker.IsEngine(dominoId))
        {
            if (!gameplayManager.DominoTracker.SelectedDomino.HasValue)
            {
                return;
            }
            
            if (gameplayManager.TurnManager.GetPlayerTurnStatus(senderClientId).HasPlayerAddedTrack)
            {
                // a track has already been added this turn
                return;
            }

            // TODO: how is it decided that the domino is played on the engine? Is it the first domino played? Is it the highest double? Is it the highest double that is played first?
            var playerTurnStation = gameplayManager.DominoTracker.GetTurnStationByClientId(senderClientId);
            gameplayManager.DominoTracker.PlayDomino(senderClientId, gameplayManager.DominoTracker.SelectedDomino.Value,
                playerTurnStation.Tracks.Count);
            gameplayManager.TurnManager.GetPlayerTurnStatus(senderClientId).PlayerAddedTrack();

            int selectedDominoId = gameplayManager.DominoTracker.SelectedDomino.Value;
            gameplayManager.DominoTracker.SetSelectedDomino(null);

            JsonContainer stationContainer = new JsonContainer(playerTurnStation);
            SelectEngineDominoClientRpc(selectedDominoId, stationContainer, SendToClientSender(serverRpcParams));
        }
        else
        {
            if (!gameplayManager.DominoTracker.SelectedDomino.HasValue)
            {
                Debug.Log("A track has already been added.");
                return;
            }

            // track domino was clicked

            // TODO: compare the domino to the last domino on the track

            var playerTurnStation = gameplayManager.DominoTracker.GetTurnStationByClientId(senderClientId);
            int trackIndex = playerTurnStation.GetTrackIndexByDominoId(dominoId).Value;
            gameplayManager.DominoTracker.PlayDomino(senderClientId, gameplayManager.DominoTracker.SelectedDomino.Value,
                trackIndex);
            gameplayManager.TurnManager.GetPlayerTurnStatus(senderClientId).PlayerMadeMove();
            
            int selectedDominoId = gameplayManager.DominoTracker.SelectedDomino.Value;
            gameplayManager.DominoTracker.SetSelectedDomino(null);

            // get the track index and pass it to the client to move the domino to the track
            JsonContainer stationContainer = new JsonContainer(playerTurnStation);
            SelectTrackDominoClientRpc(selectedDominoId, trackIndex, stationContainer,
                SendToClientSender(serverRpcParams));
        }
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
    private void SelectEngineDominoClientRpc(int selectedDominoId, JsonContainer tracksWithDominoIds,
        ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Engine domino clicked");

        gameplayManager.ClientAddSelectedToNewTrack(selectedDominoId,
            tracksWithDominoIds.GetDeserializedTrackDominoIds());
    }

    [ClientRpc]
    private void SelectTrackDominoClientRpc(int selectedDominoId, int trackIndex, JsonContainer tracksWithDominoIds,
        ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Track domino clicked");

        gameplayManager.ClientAddSelectedDominoToTrack(selectedDominoId, trackIndex,
            tracksWithDominoIds.GetDeserializedTrackDominoIds());
    }

    [ClientRpc]
    private void PlayerReadyClientRpc(ulong clientIdForTurn)
    {
        Debug.Log("Group turn complete");

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

    private ClientRpcParams SendToClientSender(ServerRpcParams serverRpcParams) => new ClientRpcParams
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