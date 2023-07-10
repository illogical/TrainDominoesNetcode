using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button serverButton;

    private void Awake()
    {
        serverButton.onClick.AddListener(OnServerButtonClicked);
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
    }

    private void OnServerButtonClicked()
    {
        NetworkManager.Singleton.StartServer();
    }

    private void OnHostButtonClicked()
    {
        Debug.Log("Starting host");
        NetworkManager.Singleton.StartHost();
    }

    private void OnClientButtonClicked()
    {
        Debug.Log("Starting client");
        NetworkManager.Singleton.StartClient();
    }
}
