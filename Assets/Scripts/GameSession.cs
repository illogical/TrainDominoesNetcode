using Assets.Scripts.Game;
using Assets.Scripts.Game.States;
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
    private List<ulong> playersCompletedFirstTurn = new List<ulong>();  // players who have completed their first turn but may not have laid down a track
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
    public void DrawInitialDominoesServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (gameplayManager.DominoTracker.GetPlayerDominoes(serverRpcParams.Receive.SenderClientId).Count > 0)
        {
            Debug.LogError($"Player {serverRpcParams.Receive.SenderClientId} already has dominoes.");
            return;
        }

        var dominoIds = gameplayManager.DrawPlayerDominoes(serverRpcParams.Receive.SenderClientId);

        DrawDominoesClientRpc(dominoIds, SendToClientSender(serverRpcParams));
    }

    [ClientRpc]
    private void DrawDominoesClientRpc(int[] dominoIds, ClientRpcParams clientRpcParams)
    {
        Debug.Log($"Received {dominoIds.Length} dominoes");

        OnPlayerDrewFromPile?.Invoke(this, dominoIds);
        gameplayManager.ClientDisplayPlayerDominoes(dominoIds);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DrawDominoServerRpc(ServerRpcParams serverRpcParams = default)
    {
        var dominoId = gameplayManager.DrawPlayerDomino(serverRpcParams.Receive.SenderClientId);

        DrawDominoClientRpc(dominoId, SendToClientSender(serverRpcParams));
    }

    [ClientRpc]
    private void DrawDominoClientRpc(int dominoId, ClientRpcParams clientRpcParams)
    {
        var dominoes = new int[] { dominoId };
        OnPlayerDrewFromPile?.Invoke(this, dominoes);

        // TODO: different animation for drawing a single domino
        //gameplayManager.DisplayPlayerDominoes(dominoes);
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if(gameplayManager.TurnManager.IsGroupTurn)
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
            gameplayManager.TurnManager.CompleteLaidFirstTrack(clientId);
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

        // first player to draw will set whose turn it is (set to the first player who joined so the host)
        gameplayManager.TurnManager.AddPlayer(clientId);

        // TODO: Need to know how many dominoes have been played or which is the player's track or null via a StationManager. If 0 then this player is ending their first turn without laying any dominoes down
        //gameplayManager.SetPlayerLaidFirstTrack(clientId);

        if (readyPlayers.Count == ((MyNetworkManager)NetworkManager.Singleton).Players.Count)
        {
            // this is the last player to end their turn

            gameplayManager.TurnManager.CompleteGroupTurn();

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

        // TODO: submit the dominoes that were laid down to the server and add to a new station track
        // TODO: validate that this is not being run a 2nd time for the same player

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
    private void StartNextPlayerTurnClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("StartNextPlayerTurnClientRpc");
        gameplayManager.StartPlayerTurn(NetworkManager.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SelectDominoServerRpc(int dominoId, ServerRpcParams serverRpcParams = default)
    {
        // TODO: the server needs to decide if the domino can be played. Is it this player's turn? Is it a valid domino to play? Is it a valid track to play on?

        // decide if this was a player domino, station domino, or engine domino
        if (gameplayManager.DominoTracker.IsPlayerDomino(serverRpcParams.Receive.SenderClientId, dominoId))
        {
            // tell client to select the domino
            SelectPlayerDominoClientRpc(dominoId, gameplayManager.DominoTracker.SelectedDomino ?? -1, SendToClientSender(serverRpcParams));
            gameplayManager.ServerSelectPlayerDomino(dominoId);
        }
        else if (gameplayManager.DominoTracker.IsEngine(dominoId)) // TODO: && gameplayManager.CompareDominoToEngine(dominoId))
        {
            if(!gameplayManager.DominoTracker.SelectedDomino.HasValue)
            {
                return;
            }

            // TODO: how is it decided that the domino is played on the engine? Is it the first domino played? Is it the highest double? Is it the highest double that is played first?
            gameplayManager.DominoTracker.PlayDomino(serverRpcParams.Receive.SenderClientId, gameplayManager.DominoTracker.SelectedDomino.Value, gameplayManager.DominoTracker.Station.Tracks.Count);
            int selectedDominoId = gameplayManager.DominoTracker.SelectedDomino.Value;
            gameplayManager.DominoTracker.SetSelectedDomino(null);

            SelectEngineDominoClientRpc(selectedDominoId, gameplayManager.DominoTracker.Station.GetTracksWithDominoes(), SendToClientSender(serverRpcParams));
        }
        else
        {
            if (!gameplayManager.DominoTracker.SelectedDomino.HasValue)
            {
                return;
            }
            // track domino was clicked

            // TODO: compare the domino to the last domino on the track
            //gameplayManager.DominoTracker.Station.GetTrackByDominoId(dominoId);

            gameplayManager.DominoTracker.PlayDomino(serverRpcParams.Receive.SenderClientId, gameplayManager.DominoTracker.SelectedDomino.Value, gameplayManager.DominoTracker.Station.Tracks.Count);
            int selectedDominoId = gameplayManager.DominoTracker.SelectedDomino.Value;
            gameplayManager.DominoTracker.SetSelectedDomino(null);

            // get the track index and pass it to the client to move the domino to the track
            int trackIndex = gameplayManager.DominoTracker.Station.GetTrackIndexByDominoId(dominoId).Value;
            SelectTrackDominoClientRpc(selectedDominoId, trackIndex, gameplayManager.DominoTracker.Station.GetTracksWithDominoes(), SendToClientSender(serverRpcParams));
        }        
    }


    [ClientRpc]
    private void SelectPlayerDominoClientRpc(int newlySelectedDominoId, int currentlySelectedDominoId, ClientRpcParams clientRpcParams = default)
    {
        // cannot serialize null
        int? selectedDominoId = currentlySelectedDominoId < 0 ? null : currentlySelectedDominoId;

        gameplayManager.ClientSelectPlayerDomino(newlySelectedDominoId, selectedDominoId);
    }

    [ClientRpc]
    private void SelectEngineDominoClientRpc(int selectedDominoId, int[][] tracksWithDominoIds, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Engine domino clicked");

        gameplayManager.ClientAddSelectedToNewTrack(selectedDominoId, tracksWithDominoIds);
    }

    [ClientRpc]
    private void SelectTrackDominoClientRpc(int selectedDominoId, int trackIndex, int[][] tracksWithDominoIds, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Track domino clicked");

        gameplayManager.ClientAddSelectedDominoToTrack(selectedDominoId, trackIndex, tracksWithDominoIds);
    }

    [ClientRpc]
    private void PlayerReadyClientRpc(ulong clientIdForTurn)
    {
        Debug.Log("Group turn complete");

        if(clientIdForTurn == NetworkManager.Singleton.LocalClientId)
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

    private ClientRpcParams SendToClientSender(ServerRpcParams serverRpcParams) => new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId } } };
    private ClientRpcParams SendToClient(ulong targetClientId) => new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetClientId } } };

    [ServerRpc(RequireOwnership = false)]
    internal void PlayerJoinedServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // gameplayManager.TurnManager.AddPlayer(serverRpcParams.Receive.SenderClientId);

        // TODO: handle player removal (this list is also tracked on the DominoPlayer object)
    }
}