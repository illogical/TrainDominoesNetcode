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

    public LoopbackRelay(AuthoritativeMatchCoordinator host)
    {
        Host = host;
        foreach (ulong id in host.Participants)
        {
            ulong peer = id;
            clients[peer] = new ClientSession(request => SendCommand(peer, request));
        }
    }

    public ClientSession this[ulong peer] => clients[peer];
    public IEnumerable<ulong> Peers => clients.Keys;
    public ulong PeerForSeat(int seat) => clients.First(c => c.Value.View.Player == seat).Key;

    /// <summary>The explicit synchronization path: a current projection delivered directly, never held.</summary>
    public void Synchronize(ulong peer) => clients[peer].Receive(DecodeProjection(Encode(Host.GetProjection(peer))), true);

    /// <summary>Releases each client's local presentation mask as if its animations finished.</summary>
    public void PresentAll()
    {
        foreach (var client in clients.Values) client.CompletePresentation(client.PresentationGeneration);
    }

    public void ReleaseCommands(IEnumerable<Message> order = null) => Release(HeldCommands, order, DeliverCommand);
    public void ReleaseProjections(IEnumerable<Message> order = null) => Release(HeldProjections, order, DeliverProjection);

    private void SendCommand(ulong peer, CommandRequest request)
    {
        var message = new Message { Peer = peer, Json = Encode(request) };
        if (HoldCommands) HeldCommands.Add(message);
        else DeliverCommand(message);
    }

    private void DeliverCommand(Message command)
    {
        foreach (var packet in Host.Submit(command.Peer, DecodeCommand(command.Json)))
        {
            var message = new Message { Peer = packet.Recipient, Json = Encode(packet.Packet) };
            if (HoldProjections) HeldProjections.Add(message);
            else DeliverProjection(message);
        }
    }

    private void DeliverProjection(Message projection) => clients[projection.Peer].Receive(DecodeProjection(projection.Json));

    private static void Release(List<Message> held, IEnumerable<Message> order, Action<Message> deliver)
    {
        var batch = (order ?? held).ToList();
        held.Clear();
        foreach (var message in batch) deliver(message);
    }

    // The relay fails loudly: a codec rejection here means a fixture produced an invalid payload.
    private static string Encode(CommandRequest value) => Codec.TryEncode(value, out var json, out var error) ? json : throw new InvalidOperationException(error);
    private static string Encode(ProjectionPacket value) => Codec.TryEncode(value, out var json, out var error) ? json : throw new InvalidOperationException(error);
    private static CommandRequest DecodeCommand(string json) => Codec.TryDecodeCommand(json, out var value, out var error) ? value : throw new InvalidOperationException(error);
    private static ProjectionPacket DecodeProjection(string json) => Codec.TryDecodeProjection(json, out var value, out var error) ? value : throw new InvalidOperationException(error);
}
