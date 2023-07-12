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
    public void PlaceEngineDominoClientRpc(int dominoId, ClientRpcParams clientRpcParams = default)
    {
        // create a mesh and place it on the side of the screen
        gameplayManager.CreateAndPlaceEngine(dominoId);
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
    public void DrawDominoesClientRpc(int[] dominoIds, ClientRpcParams clientRpcParams)
    {
        Debug.Log($"Received {dominoIds.Length} dominoes");

        OnPlayerDrewFromPile?.Invoke(this, dominoIds);
        gameplayManager.DisplayPlayerDominoes(dominoIds);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DrawDominoServerRpc(ServerRpcParams serverRpcParams = default)
    {
        var dominoId = gameplayManager.DrawPlayerDomino(serverRpcParams.Receive.SenderClientId);

        DrawDominoClientRpc(dominoId, SendToClientSender(serverRpcParams));
    }

    [ClientRpc]
    public void DrawDominoClientRpc(int dominoId, ClientRpcParams clientRpcParams)
    {
        var dominoes = new int[] { dominoId };
        OnPlayerDrewFromPile?.Invoke(this, dominoes);

        // TODO: different animation for drawing a single domino
        //gameplayManager.DisplayPlayerDominoes(dominoes);
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndFirstTurnServerRpc(ServerRpcParams serverRpcParams = default)
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

            PlayerReadyClientRpc(gameplayManager.TurnManager.CurrentPlayerId.Value);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        //TODO: gameplayManager.TurnManager.IncrementTurn(); when ending a typical turn
    }

    [ServerRpc(RequireOwnership = false)]
    public void SelectDominoServerRpc(int dominoId, ServerRpcParams serverRpcParams = default)
    {

        // TODO: the server needs to decide if the domino can be played. Is it this player's turn? Is it a valid domino to play? Is it a valid track to play on?

        // decide if this was a player domino, station domino, or engine domino
        if (gameplayManager.DominoTracker.IsPlayerDomino(serverRpcParams.Receive.SenderClientId, dominoId))
        {
            // tell client to select the domino
            SelectPlayerDominoClientRpc(dominoId, SendToClientSender(serverRpcParams));
        }
        else if (gameplayManager.DominoTracker.IsEngine(dominoId))
        {
            SelectEngineDominoClientRpc(dominoId, SendToClientSender(serverRpcParams));
        }
        else
        {
            // track domino

            // get the track index and pass it to the client to move the domino to the track
            SelectTrackDominoClientRpc(dominoId, 0, SendToClientSender(serverRpcParams));
        }        
    }

    [ClientRpc]
    public void SelectPlayerDominoClientRpc(int dominoId, ClientRpcParams clientRpcParams = default)
    {
        gameplayManager.SelectPlayerDomino(dominoId);
    }

    [ClientRpc]
    public void SelectEngineDominoClientRpc(int dominoId, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Engine domino clicked");
        //gameplayManager.AddDominoToNewTrack(dominoId);
    }

    [ClientRpc]
    public void SelectTrackDominoClientRpc(int dominoId, int trackIndex, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Track domino clicked");
        //gameplayManager.AddDominoToTrack(dominoId, trackIndex);
    }

    [ClientRpc]
    public void PlayerReadyClientRpc(ulong clientIdForTurn)
    {
        Debug.Log("All players have ended their first turn.");

        if(clientIdForTurn == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("It is now your turn.");

            gameplayManager.StartPlayerTurn();
        }
        else
        {
            Debug.Log($"It is now player {clientIdForTurn}'s turn.");

            gameplayManager.StartAwaitingTurn();
        }

        gameplayManager.CompleteTurn();
    }

    private ClientRpcParams SendToClientSender(ServerRpcParams serverRpcParams) => new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId } } };

    [ServerRpc(RequireOwnership = false)]
    internal void PlayerJoinedServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // gameplayManager.TurnManager.AddPlayer(serverRpcParams.Receive.SenderClientId);

        // TODO: handle player removal (this list is also tracked on the DominoPlayer object)
    }
}