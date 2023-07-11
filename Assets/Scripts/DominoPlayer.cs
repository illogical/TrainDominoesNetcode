using Unity.Netcode;
using UnityEngine;

public class DominoPlayer : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if(IsOwner)
        {
            ((MyNetworkManager)NetworkManager.Singleton).LocalPlayer = this;
        }

        ((MyNetworkManager)NetworkManager.Singleton).Players.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        ((MyNetworkManager)NetworkManager.Singleton).Players.Remove(this);
        ((MyNetworkManager)NetworkManager.Singleton).LocalPlayer = null;
    }
}
