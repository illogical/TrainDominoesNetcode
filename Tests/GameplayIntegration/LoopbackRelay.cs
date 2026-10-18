using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Assets.Scripts.Game.Integration;

/// <summary>
/// In-process stand-in for GameSession's RPC boundary: real ClientSession instances talk to a real
/// AuthoritativeMatchCoordinator through an encode/decode step. Delivery can be held, dropped,
/// duplicated or reordered to simulate faults. Simulated faults are not live transport evidence.
/// </summary>
/// <remarks>Payloads pass through the production <see cref="WireCodec"/>; System.Text.Json stands in for
/// JsonUtility, which exists only inside Unity. The Unity batch wire check covers JsonUtility itself.</remarks>
internal sealed class LoopbackRelay
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
    /// <summary>The production codec over System.Text.Json.</summary>
    public static readonly WireCodec Codec = new WireCodec(v => JsonSerializer.Serialize(v, v.GetType(), Options), (json, type) => JsonSerializer.Deserialize(json, type, Options));
    private readonly Dictionary<ulong, ClientSession> clients = new Dictionary<ulong, ClientSession>();
    private readonly Dictionary<ClientSession, ulong> transportOf = new Dictionary<ClientSession, ulong>();
    /// <summary>Transports whose connection has dropped; nothing is delivered to them.</summary>
    public HashSet<ulong> Disconnected { get; } = new HashSet<ulong>();

    public sealed class Message
    {
        public ulong Peer;
        public string Json;
    }

    public AuthoritativeMatchCoordinator Host { get; }
    public bool HoldCommands { get; set; }
    public bool HoldProjections { get; set; }
    /// <summary>Commands sent by Peer, waiting for host delivery.</summary>
    public List<Message> HeldCommands { get; } = new List<Message>();
    /// <summary>Projections addressed to Peer, waiting for client delivery.</summary>
    public List<Message> HeldProjections { get; } = new List<Message>();

    /// <summary>Raised for every projection a client receives, with the exact JSON that crossed the boundary.</summary>
    public event Action<ulong, ProjectionPacket, string> Delivered;

    public LoopbackRelay(AuthoritativeMatchCoordinator host)
    {
        Host = host;
        foreach (ulong id in host.Participants)
        {
            ClientSession client = null;
            // The client's transport can change on reconnect, so look it up at send time.
            client = new ClientSession(request => SendCommand(transportOf[client], request));
            clients[id] = client;
            transportOf[client] = id;
        }
    }

    public ClientSession this[ulong peer] => clients[peer];
    public IEnumerable<ulong> Peers => clients.Keys;
    public ulong PeerForSeat(int seat) => clients.First(c => c.Value.View.Player == seat).Key;

    /// <summary>The explicit synchronization path: a current projection delivered directly, never held.</summary>
    public void Synchronize(ulong peer) => Receive(peer, Encode(Host.GetProjection(peer)), true);

    /// <summary>
    /// Mirrors GameSession.SynchronizeServerRpc: the client's pending request is resubmitted with its original
    /// identity, its packets follow normal (possibly held) delivery, then a current projection arrives directly.
    /// </summary>
    public void Resynchronize(ulong peer)
    {
        var pending = clients[peer].Pending;
        if (pending != null) Dispatch(Host.Submit(peer, DecodeCommand(Encode(pending))));
        Synchronize(peer);
    }

    /// <summary>
    /// Adversarial path: a payload sent straight to the host, bypassing ClientSession's local gating, as a
    /// modified client could. Returns the outcome addressed to the sender.
    /// </summary>
    public CommandOutcome SubmitRaw(ulong peer, CommandRequest request)
    {
        var packets = Host.Submit(peer, DecodeCommand(Encode(request)));
        Dispatch(packets);
        return packets.FirstOrDefault(p => p.Recipient == peer && p.Packet.Outcome != null)?.Packet.Outcome;
    }

    /// <summary>Mirrors a dropped connection: the client becomes unavailable and the host suspends the match.</summary>
    public void Disconnect(ulong peer)
    {
        Disconnected.Add(peer);
        clients[peer].SetAvailable(false);
        Dispatch(Host.Suspend(true));
    }

    /// <summary>
    /// Mirrors GameSession.OnNetworkSpawn plus ReclaimSeatServerRpc: the same ClientSession comes back on a new
    /// transport, presents its credential (or <paramref name="claim"/>), resubmits any unanswered request, then
    /// synchronizes. A rejected claim delivers nothing.
    /// </summary>
    public ReclaimError Reconnect(ulong oldPeer, ulong newPeer, ReclaimRequest claim = null)
    {
        var client = clients[oldPeer];
        var credential = claim ?? client.Credential;
        var pending = client.Pending;
        clients.Remove(oldPeer);
        clients[newPeer] = client;
        transportOf[client] = newPeer;
        client.BeginConnection();
        var result = Host.Reclaim(newPeer, DecodeReclaim(Encode(credential)), id => !Disconnected.Contains(id) && clients.ContainsKey(id));
        if (result != ReclaimError.None) return result;
        Disconnected.RemoveWhere(id => !Host.Participants.Contains(id));
        if (pending != null) Dispatch(Host.Submit(newPeer, DecodeCommand(Encode(pending))));
        Synchronize(newPeer);
        return result;
    }

    /// <summary>Delivers host-originated packets (for example suspension) through normal, holdable delivery.</summary>
    public void Dispatch(IEnumerable<RecipientPacket> packets)
    {
        // Like GameSession.Dispatch, packets for transports that are not connected clients are not delivered.
        foreach (var packet in packets.Where(p => clients.ContainsKey(p.Recipient) && !Disconnected.Contains(p.Recipient)))
        {
            var message = new Message { Peer = packet.Recipient, Json = Encode(packet.Packet) };
            if (HoldProjections) HeldProjections.Add(message);
            else DeliverProjection(message);
        }
    }

    /// <summary>Releases each client's local presentation mask as if its animations finished.</summary>
    public void PresentAll(Func<ulong, bool> skip = null)
    {
        foreach (var pair in clients)
            if (skip == null || !skip(pair.Key)) pair.Value.CompletePresentation(pair.Value.PresentationGeneration);
    }

    public void ReleaseCommands(IEnumerable<Message> order = null) => Release(HeldCommands, order, DeliverCommand);
    public void ReleaseProjections(IEnumerable<Message> order = null) => Release(HeldProjections, order, DeliverProjection);

    private void SendCommand(ulong peer, CommandRequest request)
    {
        var message = new Message { Peer = peer, Json = Encode(request) };
        if (HoldCommands) HeldCommands.Add(message);
        else DeliverCommand(message);
    }

    private void DeliverCommand(Message command) => Dispatch(Host.Submit(command.Peer, DecodeCommand(command.Json)));

    private void DeliverProjection(Message projection) => Receive(projection.Peer, projection.Json, false);

    private void Receive(ulong peer, string json, bool synchronization)
    {
        if (!clients.ContainsKey(peer) || Disconnected.Contains(peer)) return; // Lost with the old connection.
        var packet = DecodeProjection(json);
        Delivered?.Invoke(peer, packet, json);
        clients[peer].Receive(packet, synchronization);
    }

    private static void Release(List<Message> held, IEnumerable<Message> order, Action<Message> deliver)
    {
        var batch = (order ?? held).ToList();
        held.Clear();
        foreach (var message in batch) deliver(message);
    }

    // The relay fails loudly: a codec rejection here means a fixture produced an invalid payload.
    private static string Encode(CommandRequest value) => Codec.TryEncode(value, out var json, out var error) ? json : throw new InvalidOperationException(error);
    private static string Encode(ProjectionPacket value) => Codec.TryEncode(value, out var json, out var error) ? json : throw new InvalidOperationException(error);
    private static string Encode(ReclaimRequest value) => Codec.TryEncode(value, out var json, out var error) ? json : throw new InvalidOperationException(error);
    private static ReclaimRequest DecodeReclaim(string json) => Codec.TryDecodeReclaim(json, out var value, out var error) ? value : throw new InvalidOperationException(error);
    private static CommandRequest DecodeCommand(string json) => Codec.TryDecodeCommand(json, out var value, out var error) ? value : throw new InvalidOperationException(error);
    private static ProjectionPacket DecodeProjection(string json) => Codec.TryDecodeProjection(json, out var value, out var error) ? value : throw new InvalidOperationException(error);
}
