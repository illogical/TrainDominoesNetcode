using System;
using System.Linq;
using System.Text.Json;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;
internal static class CommandBoundaryChecks
{
    private static int assertions;
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    private static CommandRequest Request(AuthoritativeMatchCoordinator h, ulong s, string id, CommandKind kind) => new CommandRequest { RequestId = id, Kind = kind, Expected = h.GetProjection(s).View.Token };
    public static void Run()
    {
        int decks = 0;
        var h = new AuthoritativeMatchCoordinator("test", new ulong[] { 50, 10 }, deckSupplier: round => { decks++; return DominoCatalog.All.Where(d => d.Id != DominoCatalog.EngineForRound(round)).OrderBy(d => d.High == 12 ? 1 : 0).Select(d => d.Id); });
        Check(h.GetProjection(10).View.Player == 0 && h.GetProjection(50).View.Player == 1, "sorted seats");
        Check(h.Submit(99, new CommandRequest { RequestId = "bad" })[0].Packet.Outcome.Error == RuleError.UnknownPlayer, "authentication");
        Check(h.Submit(10, null)[0].Packet.Outcome.Error == RuleError.InvalidCommand, "malformed");
        var ready = Request(h, 10, "ready", CommandKind.Ready);
        Check(h.Submit(10, ready).First(p => p.Recipient == 10).Packet.Outcome.Accepted, "ready accepted");
        long rev = h.GetProjection(10).View.Token.Revision;
        Check(h.Submit(10, Request(h, 10, "noop", CommandKind.Ready)).First(p => p.Recipient == 10).Packet.Outcome.Accepted && h.GetProjection(10).View.Token.Revision == rev, "no-op acknowledgment");
        var retry = h.Submit(10, ready);
        Check(retry.Count == 1 && retry[0].Packet.Outcome.Accepted && retry[0].Packet.Events.Length == 0, "retry without events");
        ready.Kind = CommandKind.Draw;
        Check(h.Submit(10, ready)[0].Packet.Outcome.Error == RuleError.InvalidCommand, "conflicting identity");
        var start = h.Submit(50, Request(h, 50, "ready", CommandKind.Ready));
        Check(start.Count == 4 && start.Where(p => p.Recipient == 10).Select(p => p.Packet.View.Token.Revision).Distinct().Count() == 2, "ordered readiness and setup");
        Check(decks == 1 && h.GetProjection(10).View.Phase == MatchPhase.Playing, "start once");
        Check(h.Submit(10, new CommandRequest { RequestId = "stale", Kind = CommandKind.Draw, Expected = ready.Expected })[0].Packet.Outcome.Error == RuleError.StalePosition, "submitted token preserved");
        Check(h.Submit(50, Request(h, 50, "wrong", CommandKind.Draw))[0].Packet.Outcome.Error == RuleError.NotYourTurn, "wrong turn");
        Check(h.GetProjection(10).View.Allowed.CanDraw, "fixture can draw");
        var drawRequest = Request(h, 10, "draw", CommandKind.Draw);
        var draw = h.Submit(10, drawRequest);
        Check(draw.First(p => p.Recipient == 10).Packet.Outcome.Accepted, "draw accepted");
        int count = h.GetProjection(10).View.Hand.Length;
        Check(h.Submit(10, drawRequest).Count == 1 && h.GetProjection(10).View.Hand.Length == count, "duplicate draw effect");
        var ownEvents = draw.First(p => p.Recipient == 10).Packet.Events;
        var otherEvents = draw.First(p => p.Recipient == 50).Packet.Events;
        Check(ownEvents.Any(e => e.DominoId >= 0) && otherEvents.All(e => e.DominoId == -1), "private draw");
        Check(otherEvents.All(e => ownEvents.Any(p => p.SourceIndex == e.SourceIndex && p.Kind == e.Kind)), "event identities");
        var options = new JsonSerializerOptions { IncludeFields = true };
        string json = JsonSerializer.Serialize(draw.First(p => p.Recipient == 50).Packet, options);
        Check(!json.Contains("Hands") && !json.Contains("Deck"), "no raw snapshot");
        Check(JsonSerializer.Deserialize<ProjectionPacket>(json, options).View.Hand.SequenceEqual(h.GetProjection(50).View.Hand), "wire roundtrip");
        h.Suspend(true);
        Check(h.Submit(10, Request(h, 10, "suspended", CommandKind.Pass))[0].Packet.Outcome.Error == RuleError.Suspended, "suspension");
        Check(h.GetProjection(10).View.Hand.Length == count && decks == 1, "rejections preserve state");
        var draft = new AuthoritativeMatchCoordinator("draft", new ulong[] { 10, 50 }, settings: new MatchSettings(CommitmentMode.Draft), deckSupplier: round => DominoCatalog.All.Where(d => d.Id != DominoCatalog.EngineForRound(round)).Select(d => d.Id));
        draft.Submit(10, Request(draft, 10, "ready", CommandKind.Ready));
        draft.Submit(50, Request(draft, 50, "ready", CommandKind.Ready));
        var legal = draft.GetProjection(10).View.LegalPlacements.First();
        var place = Request(draft, 10, "place", CommandKind.Place); place.DominoId = legal.DominoId; place.Train = legal.Train;
        var provisional = draft.Submit(10, place);
        Check(provisional.First(p => p.Recipient == 10).Packet.Outcome.Accepted, "draft accepted");
        Check(draft.GetProjection(10).View.Trains.Sum(t => t.Placements.Length) == 1 && draft.GetProjection(50).View.Trains.All(t => t.Placements.Length == 0), "draft board privacy");
        Check(provisional.First(p => p.Recipient == 50).Packet.Events.Length == 0, "draft event privacy");
        int draftCount = draft.GetProjection(10).View.Hand.Length;
        draft.Submit(10, place);
        Check(draft.GetProjection(10).View.Hand.Length == draftCount, "duplicate placement has one effect");
        draft.Submit(10, Request(draft, 10, "seal", CommandKind.EndTurn));
        Check(draft.GetProjection(50).View.Trains.Sum(t => t.Placements.Length) == 1, "seal publishes board");
        var eight = new AuthoritativeMatchCoordinator("eight", Enumerable.Range(0, 8).Select(i => (ulong)i));
        foreach (ulong id in Enumerable.Range(0, 8).Select(i => (ulong)i)) eight.Submit(id, Request(eight, id, "ready", CommandKind.Ready));
        Check(eight.GetProjection(0).View.Trains.Length == 9, "eight seats have nine trains");
        Console.WriteLine("Command boundary: " + assertions + " assertions passed.");
    }
}
