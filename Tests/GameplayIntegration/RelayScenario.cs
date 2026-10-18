using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

/// <summary>
/// A seeded match played by real ClientSessions through LoopbackRelay, with invariants checked on every
/// delivered payload. Fixtures search fixed seeds for the situation they need, so they stay deterministic
/// without hand-built decks. Shared by the NET-001, NET-005 and NET-006 checks.
/// </summary>
internal sealed class RelayScenario
{
    /// <summary>One PresentationRequested call: what a client would animate.</summary>
    public sealed class Presentation
    {
        public string Match;
        public long Revision;
        public bool Reconcile;
        public PresentationEventDto[] Events;
    }

    private readonly Random random;
    public LoopbackRelay Relay { get; }
    public AuthoritativeMatchCoordinator Host => Relay.Host;
    public int Seed { get; }
    public int Deliveries { get; private set; }
    /// <summary>Privacy or shape violations seen on any delivered payload. Checks assert this stays empty.</summary>
    public List<string> Violations { get; } = new List<string>();
    public Dictionary<ulong, List<Presentation>> Presentations { get; } = new Dictionary<ulong, List<Presentation>>();
    /// <summary>Peers whose animations lag: they finish presenting only when they must act.</summary>
    public HashSet<ulong> Slow { get; } = new HashSet<ulong>();

    private RelayScenario(int players, int seed, CommitmentMode mode, AnnouncementPenalty penalty)
    {
        Seed = seed;
        random = new Random(seed);
        var deckRandom = new Random(seed * 7919 + 1);
        var host = new AuthoritativeMatchCoordinator("s" + seed, Enumerable.Range(1, players).Select(i => (ulong)(i * 10)),
            seed % players, new MatchSettings(mode, penalty), round => Shuffled(round, deckRandom));
        Relay = new LoopbackRelay(host);
        Relay.Delivered += CheckPayload;
        foreach (ulong peer in Relay.Peers)
        {
            var list = Presentations[peer] = new List<Presentation>();
            Relay[peer].PresentationRequested += (old, next, events, generation, reconcile) =>
                list.Add(new Presentation { Match = next.Token.MatchId, Revision = next.Token.Revision, Reconcile = reconcile, Events = events });
        }
    }

    /// <summary>Synchronizes every peer and readies them into the first round.</summary>
    public static RelayScenario Start(int players, int seed, CommitmentMode mode = CommitmentMode.Immediate, AnnouncementPenalty penalty = AnnouncementPenalty.DrawOneTile)
    {
        var scenario = new RelayScenario(players, seed, mode, penalty);
        foreach (ulong peer in scenario.Relay.Peers) scenario.Relay.Synchronize(peer);
        foreach (ulong peer in scenario.Relay.Peers) scenario.Relay[peer].Ready();
        scenario.Relay.PresentAll();
        return scenario;
    }

    /// <summary>Plays seeded matches until one reaches the condition; fixed seeds keep the result deterministic.</summary>
    public static RelayScenario Find(Func<int, RelayScenario> start, Func<RelayScenario, bool> condition, int seeds = 60, int maxSteps = 4000)
    {
        for (int seed = 0; seed < seeds; seed++)
        {
            var scenario = start(seed);
            if (scenario.RunUntil(() => condition(scenario), maxSteps)) return scenario;
        }
        return null;
    }

    public PlayerViewDto View(ulong peer) => Relay[peer].View;
    public PlayerViewDto HostView(ulong peer) => Host.GetProjection(peer).View;
    public ulong PeerOf(int seat) => Host.Participants[seat];
    /// <summary>Any seat's host view, for phase and public state that every seat shares.</summary>
    public PlayerViewDto Any => HostView(Host.Participants[0]);
    public ulong Active => PeerOf(HostView(Host.Participants[0]).ActivePlayer);
    public IEnumerable<ulong> Observers => Relay.Peers.Where(p => p != Active);
    public long Revision => HostView(Host.Participants[0]).Token.Revision;

    /// <summary>A request with the sender's current host token, for raw (bypassing) submissions.</summary>
    public CommandRequest Request(ulong peer, CommandKind kind, int domino = -1, TrainIdDto train = null, int target = -1, string id = null)
        => new CommandRequest { RequestId = id ?? Guid.NewGuid().ToString("N"), Expected = HostView(peer).Token, Kind = kind, DominoId = domino, Train = train, Target = target };

    /// <summary>Encoded projections for every seat: equal snapshots mean no seat's state changed.</summary>
    public string Snapshot() => string.Join("\n", Host.Participants.Select(p =>
        LoopbackRelay.Codec.TryEncode(Host.GetProjection(p), out var json, out _) ? json : "<unencodable>"));

    /// <summary>One ordinary action by the active player through its own ClientSession; never announces.</summary>
    public bool Step()
    {
        var any = HostView(Host.Participants[0]);
        if (any.Phase == MatchPhase.MatchOver) return false;
        if (any.Phase != MatchPhase.Playing)
        {
            foreach (ulong peer in Relay.Peers) if (View(peer).Allowed.CanReady) Relay[peer].Ready();
            Relay.PresentAll(p => Slow.Contains(p));
            return true;
        }
        var client = Relay[Active];
        if (Slow.Contains(Active)) client.CompletePresentation(client.PresentationGeneration);
        var view = client.View;
        bool sent;
        if (view.LegalPlacements.Length > 0)
        {
            var choice = view.LegalPlacements[random.Next(view.LegalPlacements.Length)];
            sent = client.Submit(CommandKind.Place, choice.DominoId, choice.Train);
        }
        else if (view.Allowed.CanEndTurn) sent = client.Submit(CommandKind.EndTurn);
        else if (view.Allowed.CanDraw) sent = client.Submit(CommandKind.Draw);
        else if (view.Allowed.CanPass) sent = client.Submit(CommandKind.Pass);
        else throw new InvalidOperationException("Active player has no progress action (seed " + Seed + ").");
        if (!sent) throw new InvalidOperationException("Active client could not submit (seed " + Seed + ").");
        Relay.PresentAll(p => Slow.Contains(p));
        return true;
    }

    /// <summary>For each peer, no (match, revision) is presented incrementally twice: nothing animates or applies twice.</summary>
    public bool PresentedOnce(out string reason)
    {
        foreach (var pair in Presentations)
        {
            var twice = pair.Value.Where(p => !p.Reconcile).GroupBy(p => p.Match + "@" + p.Revision).FirstOrDefault(g => g.Count() > 1);
            if (twice != null) { reason = $"peer {pair.Key} presented {twice.Key} {twice.Count()} times"; return false; }
        }
        reason = null;
        return true;
    }

    public bool RunUntil(Func<bool> condition, int maxSteps = 4000)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            if (condition()) return true;
            if (!Step()) return condition();
        }
        return condition();
    }

    /// <summary>Every peer has the host's latest revision, nothing pending, and an identical public board.</summary>
    public bool Converged(out string reason)
    {
        reason = null;
        foreach (ulong peer in Relay.Peers)
        {
            var mine = View(peer);
            var truth = HostView(peer);
            if (mine == null || mine.Token.MatchId != truth.Token.MatchId || mine.Token.Revision != truth.Token.Revision)
            { reason = $"peer {peer} at revision {mine?.Token.Revision} not {truth.Token.Revision}"; return false; }
            if (Relay[peer].Pending != null) { reason = $"peer {peer} still has a pending request"; return false; }
        }
        var views = Relay.Peers.Select(View).ToList();
        // A private draft legitimately differs for its owner: provisional tiles, hand count and the owner's marker.
        bool anyDraft = views.Any(v => v.Trains.Any(t => t.Placements.Any(p => p.IsProvisional)));
        string Public(PlayerViewDto v) => string.Join("|",
            v.Phase, v.ActivePlayer, v.BoneyardCount, v.IsSuspended,
            string.Join(",", v.Trains.Select(t => (t.Id.IsShared ? "S" : t.Id.Owner.ToString()) + (t.IsMarked && !anyDraft ? "*" : "") + ":" +
                string.Join(".", t.Placements.Where(p => !p.IsProvisional).Select(p => p.DominoId)))),
            string.Join(",", v.Totals.Select(c => c.Player + "=" + c.Count)),
            v.RoundResults.Length, string.Join(",", v.AnnouncedPlayers));
        if (views.Select(Public).Distinct().Count() != 1) { reason = "peers disagree on the public board: " + string.Join(" || ", views.Select(Public).Distinct()); return false; }
        if (!anyDraft && views.Select(v => string.Join(",", v.HandCounts.Select(c => c.Player + "=" + c.Count))).Distinct().Count() != 1)
        { reason = "peers disagree on hand counts"; return false; }
        return true;
    }

    /// <summary>Privacy and shape invariants on one delivered payload.</summary>
    private void CheckPayload(ulong peer, ProjectionPacket packet, string json)
    {
        Deliveries++;
        var v = packet.View;
        if (v == null) return;
        int me = v.Player;
        void Fail(string what) => Violations.Add($"seed {Seed} peer {peer} rev {v.Token.Revision}: {what}");

        if (v.Trains.Any(t => t.Placements.Any(p => p.IsProvisional && p.PlayedBy != me))) Fail("saw another player's provisional placement");
        if (packet.SeatSecret != null && Relay.Peers.Where(p => p != peer).Any(p => Host.GetProjection(p)?.SeatSecret == packet.SeatSecret))
            Fail("received another seat's reconnect secret");
        foreach (var e in packet.Events)
            if ((e.Kind == EventKind.TileDrawn || e.Kind == EventKind.PenaltyTileDrawn || e.Kind == EventKind.DraftChanged) && e.Player != me)
                Fail("received another player's private " + e.Kind + " event");
        if (v.Phase != MatchPhase.Playing) return;

        var counts = v.HandCounts.ToDictionary(c => c.Player, c => c.Count);
        if (!counts.TryGetValue(me, out int own) || own != v.Hand.Length) Fail("own hand count does not match own hand");
        int placed = v.Trains.Sum(t => t.Placements.Length);
        int others = counts.Where(c => c.Key != me).Sum(c => c.Value);
        if (v.Hand.Length + others + placed + v.BoneyardCount != 90) Fail("tile accounting does not total 90");

        // No ID in this payload may be a tile another player privately holds right now (same round only).
        var current = HostView(peer);
        if (current.Token.MatchId != v.Token.MatchId || current.Token.RoundIndex != v.Token.RoundIndex || current.Phase != MatchPhase.Playing) return;
        var visible = new HashSet<int>(v.Hand.Concat(v.Trains.SelectMany(t => t.Placements.Select(p => p.DominoId)))
            .Concat(packet.Events.Where(e => e.DominoId >= 0).Select(e => e.DominoId)).Concat(v.LegalPlacements.Select(p => p.DominoId)));
        if (v.RequiredDrawnTile >= 0) visible.Add(v.RequiredDrawnTile);
        foreach (ulong other in Relay.Peers.Where(p => p != peer))
        {
            var otherView = Host.GetProjection(other).View;
            // Draft placements stay in the owner's public hand, so compare only tiles the owner still truly holds.
            if (otherView.Hand.Any(visible.Contains)) Fail($"payload exposes a tile privately held by seat {otherView.Player}");
        }
    }

    private static IEnumerable<int> Shuffled(int round, Random random)
    {
        var ids = DominoCatalog.All.Where(d => d.Id != DominoCatalog.EngineForRound(round)).Select(d => d.Id).ToArray();
        for (int i = ids.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }
        return ids;
    }
}
