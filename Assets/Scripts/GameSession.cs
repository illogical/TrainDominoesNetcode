using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class GameSession : NetworkBehaviour
{
    [SerializeField] private GameplayManager gameplayManager;

    public static GameSession Instance { get; private set; }

    private List<ulong> readyPlayers = new List<ulong>();

    private void Awake()
    {
        Instance = this;
        gameplayManager.CreateDominoSet();
    }


    [ServerRpc(RequireOwnership = false)]
    public void DrawDominoesServerRpc(ServerRpcParams serverRpcParams = default)
    {
        var dominoIds = gameplayManager.DrawPlayerDominoes(serverRpcParams.Receive.SenderClientId);

        ClientRpcParams clientRpcParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId } } };
        DrawDominoesClientRpc(dominoIds, clientRpcParams);
    }

    [ClientRpc]
    public void DrawDominoesClientRpc(int[] dominoIds, ClientRpcParams clientRpcParams)
    {
        Debug.Log($"Received {dominoIds.Length} dominoes");

        gameplayManager.DisplayPlayerDominoes(dominoIds);
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // TODO: submit the dominoes that were laid down to the server and add to a new station track
        // TODO: validate that this is not being run a 2nd time for the same player

        ulong clientId = serverRpcParams.Receive.SenderClientId;

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
        // TODO: Need to know how many dominoes have been played or which is the player's track or null via a StationManager. If 0 then this player is ending their first turn without laying any dominoes down
        gameplayManager.SetPlayerLaidFirstTrack(clientId);

        if (readyPlayers.Count == ((MyNetworkManager)NetworkManager.Singleton).Players.Count)
        {
            // first player to draw will set whose turn it is (set to the first player who joined so the host)
            if (!gameplayManager.GetPlayerIdForTurn().HasValue)
            {
                gameplayManager.SetPlayerTurn(((MyNetworkManager)NetworkManager.Singleton).Players[0].OwnerClientId);
            }

            PlayerReadyClientRpc();
        }
    }

    [ClientRpc]
    public void PlayerReadyClientRpc()
    {
        Debug.Log("All players are ready");
    }
}