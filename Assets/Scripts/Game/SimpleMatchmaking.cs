using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Game
{
    /// <summary>
    /// The scene's online entry path: anonymous sign-in, then quick-join an open lobby or create one, with
    /// Unity Relay carrying the game traffic. Uses the Multiplayer Services package (Lobby + Relay).
    /// </summary>
    public class SimpleMatchmaking : MonoBehaviour
    {
        /// <summary>The game supports 2–8 seats; the lobby and Relay allocation match that limit.</summary>
        public const int MaxPlayers = 8;
        private const string JoinCodeKey = "j";
        /// <summary>Encrypted Relay transport, as used by RelayTester.</summary>
        private const string RelayConnectionType = "dtls";
        private const float HeartbeatSeconds = 15f;

        [SerializeField] private Button _startButton;

        private Lobby _connectedLobby;
        private UnityTransport _transport;
        private string _playerId;
        private bool _busy;
        private float _nextHeartbeat;
        private NetworkManager _network;
        private bool _rosterFrozen, _admissionBusy;
        private bool _publishedLock;

        // Lock Lobby discovery, never Relay allocation: returning seats still need to reconnect.
        public void FreezeAdmission()
        {
            _rosterFrozen = true;
            PublishAdmission();
        }

        private async void PublishAdmission()
        {
            if (_admissionBusy || _connectedLobby == null || _connectedLobby.HostId != _playerId || _publishedLock == _rosterFrozen) return;
            _admissionBusy = true;
            bool locked = _rosterFrozen;
            try
            {
                _connectedLobby = await LobbyService.Instance.UpdateLobbyAsync(_connectedLobby.Id, new UpdateLobbyOptions { IsLocked = locked });
                _publishedLock = locked;
            }
            catch (Exception e) { Debug.LogWarning("Could not close lobby admission: " + e.Message); }
            finally { _admissionBusy = false; }
        }

        private void NetworkStarted() { _startButton.gameObject.SetActive(false); }
        private void NetworkStopped(bool wasHost)
        {
            bool canStart = GameSession.Instance?.Client.Credential == null && _connectedLobby == null;
            _startButton.gameObject.SetActive(canStart);
            _startButton.interactable = !_busy;
        }

        private void Awake()
        {
            _transport = FindAnyObjectByType<UnityTransport>();
            _startButton.onClick.AddListener(CreateOrJoinLobby);
            _network = NetworkManager.Singleton != null ? NetworkManager.Singleton : FindAnyObjectByType<NetworkManager>();
            if (_network != null)
            {
                _network.OnClientStarted += NetworkStarted;
                _network.OnServerStarted += NetworkStarted;
                _network.OnClientStopped += NetworkStopped;
            }
        }

        public async void CreateOrJoinLobby()
        {
            // async void is required for a button handler, so every failure must be caught here.
            if (_busy) return;
            _busy = true;
            _startButton.interactable = false;
            try
            {
                await Authenticate();
                _connectedLobby = await QuickJoinLobby() ?? await CreateLobby();
                PublishAdmission();
            }
            catch (Exception e)
            {
                Debug.LogError($"Matchmaking failed: {e.Message}");
            }
            finally
            {
                _busy = false;
                if (_connectedLobby != null || _network != null && _network.IsListening) _startButton.gameObject.SetActive(false);
                else _startButton.interactable = true;
            }
        }

        /// <summary>
        /// Launch argument giving this instance its own anonymous identity. Two instances on one machine
        /// otherwise sign in as the same player and Lobby treats them as one participant.
        /// </summary>
        public const string ProfileArgument = "--ugs-profile";

        private async Task Authenticate()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                var profile = Profile(Environment.GetCommandLineArgs());
                if (profile != null) options.SetProfile(profile);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            _playerId = AuthenticationService.Instance.PlayerId;
        }

        /// <summary>The value after <see cref="ProfileArgument"/>, if any.</summary>
        public static string Profile(string[] arguments)
        {
            int index = Array.IndexOf(arguments, ProfileArgument);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        private async Task<Lobby> QuickJoinLobby()
        {
            Lobby lobby;
            try
            {
                lobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.NoOpenLobbies)
            {
                Debug.Log("No open lobby to quick-join; creating one.");
                return null;
            }

            // A lobby was joined; a failure from here on is a real error, so leave the lobby again.
            try
            {
                var allocation = await RelayService.Instance.JoinAllocationAsync(lobby.Data[JoinCodeKey].Value);
                _transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));
                NetworkManager.Singleton.StartClient();
                return lobby;
            }
            catch (Exception)
            {
                await LeaveLobby(lobby);
                throw;
            }
        }

        /// <summary>True when this instance joined someone else's lobby and can rejoin its Relay allocation.</summary>
        public bool CanRejoin => _connectedLobby != null && _connectedLobby.HostId != _playerId;

        /// <summary>
        /// NET-004: after a dropped connection, join the same lobby's Relay allocation again and restart the
        /// client. The game then reclaims the earlier seat with its seat secret.
        /// </summary>
        public async Task<bool> RejoinRelay()
        {
            if (!CanRejoin) return false;
            try
            {
                var allocation = await RelayService.Instance.JoinAllocationAsync(_connectedLobby.Data[JoinCodeKey].Value);
                _transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));
                return NetworkManager.Singleton.StartClient();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not rejoin the Relay allocation: {e.Message}");
                return false;
            }
        }

        private async Task<Lobby> CreateLobby()
        {
            // The allocation counts connections other than the host.
            var allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            var options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject> { { JoinCodeKey, new DataObject(DataObject.VisibilityOptions.Public, joinCode) } }
            };
            var lobby = await LobbyService.Instance.CreateLobbyAsync("Train Dominoes", MaxPlayers, options);
            _nextHeartbeat = Time.realtimeSinceStartup + HeartbeatSeconds;

            _transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));
            NetworkManager.Singleton.StartHost();
            return lobby;
        }

        private void Update()
        {
            // The lobby service closes a hosted lobby without heartbeats; send one every 15 seconds.
            if (_connectedLobby == null || _connectedLobby.HostId != _playerId || Time.realtimeSinceStartup < _nextHeartbeat) return;
            _nextHeartbeat = Time.realtimeSinceStartup + HeartbeatSeconds;
            PublishAdmission();
            _ = Heartbeat(_connectedLobby.Id);
        }

        private static async Task Heartbeat(string lobbyId)
        {
            try { await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId); }
            catch (Exception e) { Debug.LogWarning($"Lobby heartbeat failed: {e.Message}"); }
        }

        private async Task LeaveLobby(Lobby lobby)
        {
            try
            {
                if (lobby.HostId == _playerId) await LobbyService.Instance.DeleteLobbyAsync(lobby.Id);
                else await LobbyService.Instance.RemovePlayerAsync(lobby.Id, _playerId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not leave lobby {lobby.Id}: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (_network != null)
            {
                _network.OnClientStarted -= NetworkStarted;
                _network.OnServerStarted -= NetworkStarted;
                _network.OnClientStopped -= NetworkStopped;
            }
            _startButton.onClick.RemoveListener(CreateOrJoinLobby);
            if (_connectedLobby == null) return;
            // Best effort while shutting down; failures are logged inside LeaveLobby.
            _ = LeaveLobby(_connectedLobby);
            _connectedLobby = null;
        }
    }
}
