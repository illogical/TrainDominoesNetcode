using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DominoPlayer : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if(IsOwner)
        {
            Debug.Log("Welcome!");

            ((MyNetworkManager)NetworkManager.Singleton).Players.Add(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        ((MyNetworkManager)NetworkManager.Singleton).Players.Remove(this);
    }
}
