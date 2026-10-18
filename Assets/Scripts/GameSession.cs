using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game.Integration;
using Assets.Scripts.Game.States;
using TrainDominoesRulesEngine;
using Unity.Netcode;
using UnityEngine;

/// <summary>Transport adapter. Core state exists only on the server; clients receive their own projections.</summary>
public class GameSession : NetworkBehaviour
{
    public static GameSession Instance { get; private set; }
    [SerializeField] private GameplayManager gameplayManager;
    [SerializeField] private StateTraceLevel stateTraceLevel = StateTraceLevel.Transitions;
    [SerializeField] private CommitmentMode commitment = CommitmentMode.Immediate;
    [SerializeField] private AnnouncementPenalty announcementPenalty = AnnouncementPenalty.DrawOneTile;
    public ClientSession Client { get; private set; }
    public GameStateContext PlayerStateContext => context;
    public StateTraceLevel StateTracing
    {
        get => stateTraceLevel;
        set { stateTraceLevel = value; if (context != null) context.Diagnostics.Level = value; }
    }
    private GameStateContext context;
    private PlayerStateCoordinator states;
    private AuthoritativeMatchCoordinator authority;
    private bool hooked;
    private float requestSentAt;
    private bool disposed;
    private int decodeFailures;
    private const int MaximumDecodeResynchronizations = 3;
    /// <summary>The production wire contract; every RPC payload goes through it.</summary>
    public static readonly WireCodec Wire = new WireCodec(JsonUtility.ToJson, JsonUtility.FromJson);

    private void Awake()
    {
        Instance = this;
        Client = new ClientSession(SendCommand);
        Client.StartMatchRequested += RequestInitialMatch;
        Client.NewMatchRequested += RequestNewMatch;
        Client.SynchronizationRequested += RequestSynchronization;
        Client.ResumeRequested += RequestResume;
        Client.RequestChanged += RequestChanged;
    }
    private void Start() { InitializeClient(); }
    private void InitializeClient()
    {
        if (context != null) return;
        gameplayManager.Initialize(Client);
        context = new GameStateContext(this, gameplayManager, new GameStateDiagnostics(LogStateTrace) { Level = stateTraceLevel });
        states = new PlayerStateCoordinator(context);
        states.Start();
    }
    public override void OnNetworkSpawn()
    {
        InitializeClient();
        if (!hooked)
        {
            NetworkManager.OnClientDisconnectCallback += Disconnected;
            hooked = true;
        }
        gameplayManager.CancelPresentation();
        Client.BeginConnection();
        if (IsClient) SynchronizeServerRpc("");
    }
    private void OnValidate() { if (context != null) context.Diagnostics.Level = stateTraceLevel; }
    private void Update()
    {
        if (Client?.Pending != null && !Client.IsUncertain && Time.realtimeSinceStartup - requestSentAt > 5f)
            Client.MarkUncertain();
    }
    private void RequestChanged(object sender, EventArgs e)
    {
        if (Client.Pending != null && !Client.IsUncertain) requestSentAt = Time.realtimeSinceStartup;
        if (stateTraceLevel != StateTraceLevel.Off)
            Debug.Log($"[Command client={NetworkManager?.LocalClientId}] request={Client.Pending?.RequestId ?? "settled"} uncertain={Client.IsUncertain}", this);
    }
    private void SendCommand(CommandRequest request)
    {
        if (!IsSpawned || !IsClient) { Client.MarkUncertain(); return; }
        if (!Wire.TryEncode(request, out var json, out var error))
        {
            Debug.LogError("Command not sent: " + error, this);
            Client.MarkUncertain();
            return;
        }
        SubmitCommandServerRpc(json);
    }
    private void RequestInitialMatch() { if (IsSpawned) CreateMatchServerRpc(false); }
    private void RequestNewMatch() { if (IsSpawned) CreateMatchServerRpc(true); }
    private void RequestResume() { if (IsSpawned) ResumeServerRpc(); }
    private void RequestSynchronization()
    {
        if (!IsSpawned) return;
        // A pending request that cannot be encoded is dropped from the sync; the view still resynchronizes.
        string pending = Client.Pending != null && Wire.TryEncode(Client.Pending, out var json, out _) ? json : "";
        SynchronizeServerRpc(pending);
    }

    [ServerRpc(RequireOwnership = false)]
    private void CreateMatchServerRpc(bool replacement, ServerRpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (sender != Unity.Netcode.NetworkManager.ServerClientId)
        {
            FeedbackClientRpc("Only the host can create a match.", To(sender));
            return;
        }
        if (authority != null)
        {
            var current = authority.GetProjection(sender)?.View;
            if (!replacement || current == null || (!current.IsSuspended && current.Phase != MatchPhase.MatchOver))
            {
                FeedbackClientRpc("The current match must finish or be suspended first.", To(sender));
                return;
            }
        }
        var participants = NetworkManager.ConnectedClientsIds.OrderBy(id => id).ToArray();
        if (participants.Length < 2 || participants.Length > 8)
        {
            FeedbackClientRpc("Connect 2–8 players before starting.", To(sender));
            return;
        }
        authority = new AuthoritativeMatchCoordinator(Guid.NewGuid().ToString("N"), participants,
            UnityEngine.Random.Range(0, participants.Length), new MatchSettings(commitment, announcementPenalty));
        foreach (var id in authority.Participants) Send(id, authority.GetProjection(id));
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitCommandServerRpc(string json, ServerRpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (authority == null) { FeedbackClientRpc("The host has not created a match yet.", To(sender)); return; }
        if (!TryRead(json, out var request)) { FeedbackClientRpc("Invalid command payload.", To(sender)); return; }
        Dispatch(authority.Submit(sender, request));
    }

    [ServerRpc(RequireOwnership = false)]
    private void SynchronizeServerRpc(string pending, ServerRpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (authority == null) { FeedbackClientRpc("Host: start a match with 2–8 connected players.", To(sender)); return; }
        if (!string.IsNullOrEmpty(pending) && TryRead(pending, out var request)) Dispatch(authority.Submit(sender, request));
        var packet = authority.GetProjection(sender);
        if (packet == null) { FeedbackClientRpc("You are not seated in this match. Wait for a fresh match.", To(sender)); return; }
        Send(sender, packet, true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResumeServerRpc(ServerRpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (sender != Unity.Netcode.NetworkManager.ServerClientId || authority == null) return;
        if (!authority.Participants.All(id => NetworkManager.ConnectedClients.ContainsKey(id)))
        {
            FeedbackClientRpc("A seat is disconnected. Start a fresh match with the remaining players.", To(sender));
            return;
        }
        Dispatch(authority.Suspend(false));
        foreach (var id in authority.Participants) Send(id, authority.GetProjection(id), true);
    }

    private void Disconnected(ulong id)
    {
        if (disposed) return;
        if (IsServer && authority != null && authority.Participants.Contains(id)) Dispatch(authority.Suspend(true));
        if (id == NetworkManager.LocalClientId)
        {
            Client.SetAvailable(false);
            gameplayManager.CancelPresentation();
        }
    }

    private bool TryRead(string json, out CommandRequest request)
    {
        if (Wire.TryDecodeCommand(json, out request, out var error)) return true;
        if (stateTraceLevel != StateTraceLevel.Off) Debug.LogWarning("Rejected command payload: " + error, this);
        return false;
    }

    private void Dispatch(IReadOnlyList<RecipientPacket> packets)
    {
        foreach (var packet in packets)
            if (NetworkManager.ConnectedClients.ContainsKey(packet.Recipient)) Send(packet.Recipient, packet.Packet);
        if (!string.IsNullOrEmpty(authority.LastLifecycleError)) Debug.LogError("Round setup failed: " + authority.LastLifecycleError, this);
    }

    private void Send(ulong id, ProjectionPacket packet, bool synchronization = false)
    {
        if (!Wire.TryEncode(packet, out var json, out var error))
        {
            // Never drop silently: the recipient is told and can resynchronize once the cause is fixed.
            Debug.LogError($"Projection for client {id} not sent: {error}", this);
            FeedbackClientRpc("The host could not send an update. Try synchronizing.", To(id));
            return;
        }
        ProjectionClientRpc(json, synchronization, To(id));
    }

    private static ClientRpcParams To(ulong id) => new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { id } } };

    [ClientRpc]
    private void ProjectionClientRpc(string json, bool synchronization, ClientRpcParams rpc = default)
    {
        InitializeClient();
        if (!Wire.TryDecodeProjection(json, out var packet, out var error))
        {
            Debug.LogError("Unreadable projection: " + error, this);
            // Bounded so a persistently bad payload cannot cause a synchronization loop.
            if (++decodeFailures <= MaximumDecodeResynchronizations)
            {
                Client.SetFeedback("Received an unreadable update; resynchronizing.");
                RequestSynchronization();
            }
            else Client.SetFeedback("Updates from the host cannot be read. Check that both players run the same build.");
            return;
        }
        decodeFailures = 0;
        Client.Receive(packet, synchronization);
    }

    [ClientRpc]
    private void FeedbackClientRpc(string message, ClientRpcParams rpc = default) { Client.SetFeedback(message); }

    private void LogStateTrace(string message) { Debug.Log($"[session={GetEntityId()} client={NetworkManager?.LocalClientId}] {message}", this); }

    public override void OnNetworkDespawn()
    {
        Client.SetAvailable(false);
        gameplayManager.CancelPresentation();
        authority = null;
        if (hooked && NetworkManager != null) { NetworkManager.OnClientDisconnectCallback -= Disconnected; hooked = false; }
    }

    public override void OnDestroy()
    {
        disposed = true;
        if (hooked && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= Disconnected;
        states?.Dispose();
        context?.Dispose();
        gameplayManager?.DisposePresentation();
        if (Client != null)
        {
            Client.StartMatchRequested -= RequestInitialMatch;
            Client.NewMatchRequested -= RequestNewMatch;
            Client.SynchronizationRequested -= RequestSynchronization;
            Client.ResumeRequested -= RequestResume;
            Client.RequestChanged -= RequestChanged;
        }
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }
}
