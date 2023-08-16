using QFSW.QC;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayTester : NetworkBehaviour
{
    private const int MAX_PLAYERS = 8;

    private void Start()
    {
        SignIn();
    }

    private async void SignIn()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    [Command]
    private async void CreateRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MAX_PLAYERS - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"Join code = {joinCode}");

            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            NetworkManager.StartHost();

        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command]
    private async void JoinRelay(string joinCode)
    {
        try
        {
            Debug.Log($"Joining with join code {joinCode}");
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            NetworkManager.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
    }
}
