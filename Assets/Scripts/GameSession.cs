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
    public StateTraceLevel StateTracing { get => stateTraceLevel; set { stateTraceLevel=value; if(context!=null) context.Diagnostics.Level=value; } }
    private GameStateContext context;
    private PlayerStateCoordinator states;
    private AuthoritativeMatchCoordinator authority;
    private ulong[] matchParticipants = Array.Empty<ulong>();
    private bool hooked;
    private float requestSentAt;
    private bool disposed;
    private const int MaximumCommandCharacters = 16384;

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
        context = new GameStateContext(this, gameplayManager, new GameStateDiagnostics(LogStateTrace) { Level=stateTraceLevel });
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
    private void OnValidate() { if (context != null) context.Diagnostics.Level=stateTraceLevel; }
    private void Update()
    {
        if (Client?.Pending != null && !Client.IsUncertain && Time.realtimeSinceStartup-requestSentAt > 5f)
            Client.MarkUncertain();
    }
    private void RequestChanged(object sender, EventArgs e)
    {
        if (Client.Pending != null && !Client.IsUncertain) requestSentAt=Time.realtimeSinceStartup;
        if (stateTraceLevel != StateTraceLevel.Off)
            Debug.Log($"[Command client={NetworkManager?.LocalClientId}] request={Client.Pending?.RequestId ?? "settled"} uncertain={Client.IsUncertain}",this);
    }
    private void SendCommand(CommandRequest request)
    {
        if (!IsSpawned || !IsClient) { Client.MarkUncertain(); return; }
        SubmitCommandServerRpc(JsonUtility.ToJson(request));
    }
    private void RequestInitialMatch() { if (IsSpawned) CreateMatchServerRpc(false); }
    private void RequestNewMatch() { if (IsSpawned) CreateMatchServerRpc(true); }
    private void RequestResume() { if (IsSpawned) ResumeServerRpc(); }
    private void RequestSynchronization() { if (IsSpawned) SynchronizeServerRpc(Client.Pending == null ? "" : JsonUtility.ToJson(Client.Pending)); }

    [ServerRpc(RequireOwnership=false)]
    private void CreateMatchServerRpc(bool replacement, ServerRpcParams rpc=default)
    {
        ulong sender=rpc.Receive.SenderClientId;
        if (sender != Unity.Netcode.NetworkManager.ServerClientId) { FeedbackClientRpc("Only the host can create a match.", To(sender)); return; }
        if (authority != null)
        {
            var current=authority.GetProjection(sender)?.View;
            if (!replacement || current == null || (!current.IsSuspended && current.Phase != MatchPhase.MatchOver))
            { FeedbackClientRpc("The current match must finish or be suspended first.",To(sender)); return; }
        }
        var participants=NetworkManager.ConnectedClientsIds.OrderBy(id=>id).ToArray();
        if (participants.Length<2 || participants.Length>8) { FeedbackClientRpc("Connect 2–8 players before starting.",To(sender)); return; }
        matchParticipants=participants;
        authority=new AuthoritativeMatchCoordinator(Guid.NewGuid().ToString("N"),participants,UnityEngine.Random.Range(0,participants.Length),new MatchSettings(commitment,announcementPenalty));
        foreach (var id in participants) Send(id,authority.GetProjection(id));
    }
    [ServerRpc(RequireOwnership=false)]
    private void SubmitCommandServerRpc(string json, ServerRpcParams rpc=default)
    {
        if (authority==null) { FeedbackClientRpc("The host has not created a match yet.",To(rpc.Receive.SenderClientId)); return; }
        if (!TryRead(json,out var request)) { FeedbackClientRpc("Invalid command payload.",To(rpc.Receive.SenderClientId)); return; }
        Dispatch(authority.Submit(rpc.Receive.SenderClientId,request));
    }
    [ServerRpc(RequireOwnership=false)]
    private void SynchronizeServerRpc(string pending, ServerRpcParams rpc=default)
    {
        ulong sender=rpc.Receive.SenderClientId;
        if (authority==null) { FeedbackClientRpc("Host: start a match with 2–8 connected players.",To(sender)); return; }
        if (!string.IsNullOrEmpty(pending) && TryRead(pending,out var request)) Dispatch(authority.Submit(sender,request));
        var packet=authority.GetProjection(sender);
        if (packet==null) { FeedbackClientRpc("You are not seated in this match. Wait for a fresh match.",To(sender)); return; }
        ProjectionClientRpc(JsonUtility.ToJson(packet),true,To(sender));
    }
    [ServerRpc(RequireOwnership=false)]
    private void ResumeServerRpc(ServerRpcParams rpc=default)
    {
        if (rpc.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId || authority==null) return;
        if (!matchParticipants.All(id=>NetworkManager.ConnectedClients.ContainsKey(id)))
        { FeedbackClientRpc("A seat is disconnected. Start a fresh match with the remaining players.",To(rpc.Receive.SenderClientId)); return; }
        Dispatch(authority.Suspend(false));
        foreach(var id in matchParticipants) ProjectionClientRpc(JsonUtility.ToJson(authority.GetProjection(id)),true,To(id));
    }
    private void Disconnected(ulong id)
    {
        if (disposed) return;
        if (IsServer && authority!=null && matchParticipants.Contains(id)) Dispatch(authority.Suspend(true));
        if (id==NetworkManager.LocalClientId) { Client.SetAvailable(false); gameplayManager.CancelPresentation(); }
    }
    private static bool TryRead(string json,out CommandRequest request)
    {
        request=null;
        if (string.IsNullOrEmpty(json) || json.Length>MaximumCommandCharacters) return false;
        try { request=JsonUtility.FromJson<CommandRequest>(json); return request!=null; }
        catch(ArgumentException) { return false; }
    }
    private void Dispatch(IReadOnlyList<RecipientPacket> packets)
    {
        foreach(var packet in packets) if(NetworkManager.ConnectedClients.ContainsKey(packet.Recipient)) Send(packet.Recipient,packet.Packet);
        if (!string.IsNullOrEmpty(authority.LastLifecycleError)) Debug.LogError("Round setup failed: "+authority.LastLifecycleError,this);
    }
    private void Send(ulong id,ProjectionPacket packet) { ProjectionClientRpc(JsonUtility.ToJson(packet),false,To(id)); }
    private static ClientRpcParams To(ulong id) => new ClientRpcParams { Send=new ClientRpcSendParams { TargetClientIds=new[]{id} } };
    [ClientRpc]
    private void ProjectionClientRpc(string json,bool synchronization,ClientRpcParams rpc=default)
    {
        InitializeClient();
        Client.Receive(JsonUtility.FromJson<ProjectionPacket>(json),synchronization);
    }
    [ClientRpc]
    private void FeedbackClientRpc(string message,ClientRpcParams rpc=default) { Client.SetFeedback(message); }
    private void LogStateTrace(string message) { Debug.Log($"[session={GetEntityId()} client={NetworkManager?.LocalClientId}] {message}",this); }
    public override void OnNetworkDespawn()
    {
        Client.SetAvailable(false);
        gameplayManager.CancelPresentation();
        authority=null; matchParticipants=Array.Empty<ulong>();
        if (hooked && NetworkManager!=null) { NetworkManager.OnClientDisconnectCallback-=Disconnected; hooked=false; }
    }
    public override void OnDestroy()
    {
        disposed=true;
        if(hooked && NetworkManager!=null) NetworkManager.OnClientDisconnectCallback-=Disconnected;
        states?.Dispose(); context?.Dispose(); gameplayManager?.DisposePresentation();
        if(Client!=null)
        {
            Client.StartMatchRequested-=RequestInitialMatch; Client.NewMatchRequested-=RequestNewMatch;
            Client.SynchronizationRequested-=RequestSynchronization; Client.ResumeRequested-=RequestResume; Client.RequestChanged-=RequestChanged;
        }
        if(Instance==this) Instance=null;
        base.OnDestroy();
    }
}
