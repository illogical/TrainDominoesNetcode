using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class GameSession : NetworkBehaviour
{
    [SerializeField] private GameplayManager gameplayManager;

    public static GameSession Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            //CreateDominoSetServerRpc();
        }

        //DrawDominoesServerRpc();
    }

    [ServerRpc]
    public void CreateDominoSetServerRpc()
    {
        gameplayManager.DominoManager.CreateDominoSet();
        Debug.Log("Domino set created");
    }

    [ServerRpc(RequireOwnership = false)]
    public void DrawDominoesServerRpc(ServerRpcParams serverRpcParams = default)
    {
        gameplayManager.DominoManager.PickUpDominoes(serverRpcParams.Receive.SenderClientId, 12);
        var myDominoes = gameplayManager.DominoManager.GetPlayerDominoes(serverRpcParams.Receive.SenderClientId);
        var dominoIds = myDominoes.Select(d => d.ID).ToArray();

        Debug.Log($"{gameplayManager.DominoManager.GetDominoesRemainingCount()} dominoes remaining");

        ClientRpcParams clientRpcParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId } } };
        DrawDominoesClientRpc(dominoIds, clientRpcParams);
    }

    [ClientRpc]
    public void DrawDominoesClientRpc(int[] dominoIds, ClientRpcParams clientRpcParams)
    {
        // TODO: display the dominoes in the player's hand

        Debug.Log($"Received {dominoIds.Length} dominoes");

        
        gameplayManager.DisplayPlayerDominoes(dominoIds);
        

        //var domino = gameplayManager.DominoManager.GetDominoByID(dominoId);
        //Debug.Log($"Received domino: {domino.ID} {domino.TopScore} {domino.BottomScore}");
    }
}
