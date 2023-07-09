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
        }
    }
}
